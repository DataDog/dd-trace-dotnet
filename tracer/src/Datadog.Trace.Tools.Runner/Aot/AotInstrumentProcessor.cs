// <copyright file="AotInstrumentProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.InstrumentedAssemblyVerification;
using Datadog.Trace.Tools.Runner.Aot.Native;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Implements <c>dd-trace aot instrument</c>.
/// </summary>
internal static class AotInstrumentProcessor
{
    internal static int Process(AotInstrumentOptions options)
    {
#if NET6_0_OR_GREATER
        AotLog.Verbose = options.Verbose;
        NativeStubDiagnostics.Reset();
        var report = new AotInstrumentReport();
        try
        {
            Validate(options);
            using var host = NativeTracerHost.Create(options.NativeTracerPath, options.RuntimeVersion);

            if (options.UseEmbeddedDefinitions)
            {
                report.EmbeddedDefinitions = host.EnableEmbeddedDefinitions(options.Categories, options.TargetFramework);
            }

            if (options.DefinitionsAssembly is { } definitionsAssembly && options.DefinitionsMethod is { } definitionsMethod)
            {
                var separator = definitionsMethod.IndexOf("::", StringComparison.Ordinal);
                host.InvokeDefinitionsMethod(options.DatadogTracePath, definitionsAssembly, definitionsMethod.Substring(0, separator), definitionsMethod.Substring(separator + 2));
            }

            // CLR load order: CoreLib first (the native tracer captures the corlib reference from it), then
            // Datadog.Trace (read-only: its rewrites target the runtime host, not the application), then the assemblies.
            var corlib = options.ReferenceDirectories.Select(d => Path.Combine(d, "System.Private.CoreLib.dll")).First(File.Exists);
            host.LoadModule(corlib, writable: false);
            host.LoadModule(options.DatadogTracePath, writable: false);
            foreach (var assembly in options.Assemblies)
            {
                host.LoadModule(Path.GetFullPath(assembly), writable: true);
            }

            var probeDirectories = options.Assemblies.Select(a => Path.GetDirectoryName(Path.GetFullPath(a))!)
                                          .Concat(options.ReferenceDirectories)
                                          .Distinct(StringComparer.Ordinal)
                                          .ToList();
            host.LoadReferenceClosure(probeDirectories);
            report.ReJitProcessed = host.ProcessReJitRequests(TimeSpan.FromSeconds(1));

            Directory.CreateDirectory(options.OutputDirectory);
            foreach (var module in host.Runtime.Modules.Where(m => m.Writable))
            {
                var output = Path.Combine(options.OutputDirectory, Path.GetFileName(module.Path));
                var rewrittenMethods = module.NewBodies.Keys.Select(rid => module.Module.ResolveMethod(rid)).Where(m => m is not null).Select(DescribeForVerification).ToList();
                MethodBodies.Write(module, output, options.Neutralize);
                if (options.Verify && rewrittenMethods.Count > 0)
                {
                    var outcome = new VerificationsRunner(output, module.Path, rewrittenMethods, options.ReferenceDirectories.Concat(new[] { Path.GetDirectoryName(module.Path)! }).ToList(), failOnVerificationError: true).Run();
                    if (!outcome.IsValid)
                    {
                        report.Errors.Add($"Verification failed for {module.AssemblyName}: {outcome.FailureReason}");
                    }
                }

                report.Assemblies.Add(new AotInstrumentReport.AssemblyResult { Name = module.AssemblyName, Path = output, RewrittenMethods = module.NewBodies.Count });
                AotLog.Info($"{module.AssemblyName}: {module.NewBodies.Count} methods instrumented -> {output}");
            }
        }
        catch (Exception ex)
        {
            report.Errors.Add(ex.ToString());
            AotLog.Error($"NativeAOT instrumentation failed: {ex}");
        }
        finally
        {
            NativeObjectRoots.Clear();
        }

        report.NotImplemented = NativeStubDiagnostics.NotImplementedCallCounts.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
        report.Errors.AddRange(NativeStubDiagnostics.Errors);
        if (options.ReportPath is { } reportPath)
        {
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(report, Formatting.Indented));
        }

        return report.Errors.Count == 0 ? 0 : 1;
#else
        AotLog.Error("NativeAOT instrumentation requires the .NET 6+ build of dd-trace.");
        return 1;
#endif
    }

#if NET6_0_OR_GREATER
    private static (string Type, string Method) DescribeForVerification(dnlib.DotNet.MethodDef method)
        => (method.DeclaringType.ReflectionFullName, $"{method.Name}({string.Join(",", method.Parameters.Where(p => !p.IsHiddenThisParameter).Select(p => p.Type.FullName))})");
#endif

    private static void Validate(AotInstrumentOptions options)
    {
        foreach (var path in new[] { options.NativeTracerPath, options.DatadogTracePath }.Concat(options.Assemblies))
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}", path);
            }
        }

        if (!options.ReferenceDirectories.Any(d => File.Exists(Path.Combine(d, "System.Private.CoreLib.dll"))))
        {
            throw new ArgumentException("No reference directory contains System.Private.CoreLib.dll.");
        }
    }
}
