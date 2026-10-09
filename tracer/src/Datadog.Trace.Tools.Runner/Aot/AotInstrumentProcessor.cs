// <copyright file="AotInstrumentProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.InstrumentedAssemblyVerification;
#if NET6_0_OR_GREATER
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using dnlib.DotNet.Pdb;
#endif
using Datadog.Trace.Tools.Runner.Aot.Native;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// Implements <c>dd-trace aot instrument</c>.
/// </summary>
internal static class AotInstrumentProcessor
{
    // InstrumentationCategory of Datadog.Trace
    private const uint IastCategory = 4;
    private const uint RaspCategory = 8;

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

                // IAST and RASP also rewrite call sites (aspects), like Instrumentation.EnableCallSiteInstrumentations.
                var callSiteCategories = options.Categories & (IastCategory | RaspCategory);
                if (callSiteCategories != 0)
                {
                    report.EmbeddedCallSites = host.EnableEmbeddedCallSites(callSiteCategories, options.TargetFramework);
                    AotLog.Info($"Call site aspects: {report.EmbeddedCallSites}");
                }
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
            if (report.EmbeddedCallSites > 0)
            {
                foreach (var module in host.Runtime.Modules.Where(m => m.Writable).ToList())
                {
                    report.CallSiteMethods += host.ProcessCallSites(module);
                }

                AotLog.Info($"Methods processed for call sites: {report.CallSiteMethods}");
            }

            // IAST's hardcoded secrets: the string literals the native tracer collected from those methods.
            List<(string Location, string Value)>? userStrings = null;
            if ((options.Categories & IastCategory) != 0)
            {
                userStrings = host.TakeUserStrings();
                report.UserStrings = userStrings.Count;
                AotLog.Info($"String literals for the hardcoded secrets analysis: {userStrings.Count}");
            }

            report.ReJitProcessed = host.ProcessReJitRequests(TimeSpan.FromSeconds(1));

            Directory.CreateDirectory(options.OutputDirectory);
            var modules = host.Runtime.Modules;
            var datadogTrace = modules.First(m => !m.Writable && string.Equals(Path.GetFullPath(m.Path), Path.GetFullPath(options.DatadogTracePath), StringComparison.Ordinal)).Module;
            var typeResolver = new LoadedModulesTypeResolver(modules.Select(m => m.Module));

            // The models Datadog.Trace (de)serializes with Newtonsoft by reflection, which ILC must keep.
            report.JsonModels = JsonModelDescriptor.Write(datadogTrace, Path.Combine(options.OutputDirectory, "Datadog.Trace.Json.linker.xml"), Path.Combine(options.OutputDirectory, "Datadog.Trace.Json.rd.xml"));
            AotLog.Info($"JSON models of Datadog.Trace preserved: {report.JsonModels}");

            // The types of the application its integrations find by name, then use by reflection.
            report.ReflectionRoots = ReflectionRootDescriptor.Write(datadogTrace, modules.Select(m => (dnlib.DotNet.ModuleDef)m.Module), Path.Combine(options.OutputDirectory, "Datadog.Trace.Reflection.linker.xml"));
            AotLog.Info($"Types of the application Datadog.Trace finds by name preserved: {report.ReflectionRoots}");

            // The delegate wrappers DelegateInstrumentation instantiates at runtime with MakeGenericType.
            report.DelegateWrappers = DelegateWrapperDirectives.Write(datadogTrace, Path.Combine(options.OutputDirectory, "Datadog.Trace.Delegates.rd.xml"));
            AotLog.Info($"Delegate instrumentation wrappers preserved: {report.DelegateWrappers}");

            // Only the assemblies the native tracer rewrote are written: the others (most of the framework references a
            // publish passes) are left as they are.
            var application = modules.FirstOrDefault(m => m.Writable && options.Assemblies.Count > 0 && string.Equals(Path.GetFullPath(m.Path), Path.GetFullPath(options.Assemblies[0]), StringComparison.Ordinal));
            var writableModules = modules.Where(m => m.Writable && (m.NewBodies.Count > 0 || (m == application && options.GenerateCallTargetRegistry))).ToList();
            AotLog.Info($"{writableModules.Count}/{modules.Count(m => m.Writable)} assemblies instrumented");
            var rewrittenByModule = writableModules.ToDictionary(m => m, MethodBodies.ApplyNewBodies);

            // The composite interfaces of the reverse proxies of Datadog.Trace, which dynamic duck typing emits at runtime.
            var compositeCount = 0;
            var compositeInterfaces = application is null ? null : CompositeInterfaceAssembly.Write(datadogTrace, modules.Select(m => (dnlib.DotNet.ModuleDef)m.Module).ToList(), application.Module, options.OutputDirectory, out compositeCount);
            report.CompositeInterfaces = compositeCount;
            AotLog.Info($"Composite interfaces of duck typing reverse proxies: {report.CompositeInterfaces}");

            // Code Origin for spans: the source locations of the endpoint methods, which the PDBs give at runtime otherwise.
            var codeOriginLocations = new List<string>();
            foreach (var module in modules.Where(m => m.Writable))
            {
                report.CodeOriginLocations += CodeOriginLocations.Collect(module.Module, module.Path, codeOriginLocations);
            }

            AotLog.Info($"Source locations of endpoint methods for Code Origin: {report.CodeOriginLocations}");

            // The proxies of the duck typing constraints go to a DuckType AOT registry generated first (C1): the adapters
            // create them directly.
            CallTargetDuckTypeRegistry? duckTypeRegistry = null;
            Dictionary<dnlib.DotNet.MethodDef, List<GenericInstantiationDiscovery.Instantiation>>? instantiations = null;
            if (options.GenerateCallTargetRegistry)
            {
                // The instantiations come from all the given assemblies (the application creates the framework's generic types).
                var genericRewritten = rewrittenByModule.Values.SelectMany(m => m).Where(m => m.HasGenericParameters || m.DeclaringType.HasGenericParameters).ToList();
                instantiations = genericRewritten.Count == 0
                                     ? new Dictionary<dnlib.DotNet.MethodDef, List<GenericInstantiationDiscovery.Instantiation>>()
                                     : GenericInstantiationDiscovery.Discover(modules.Where(m => m.Writable).Select(m => (dnlib.DotNet.ModuleDef)m.Module), genericRewritten, typeResolver.Resolve);
                AotLog.Info($"Closed instantiations of generic instrumented methods: {instantiations.Values.Sum(i => i.Count)} for {instantiations.Count} methods");
                var collector = new DuckProxyRequestCollector();
                foreach (var rewritten in rewrittenByModule.Values)
                {
                    CallTargetRegistryGenerator.CollectProxyRequests(rewritten, typeResolver.Resolve, collector, instantiations);
                }

                AotLog.Info($"Duck typing proxies needed by the CallTarget adapters: {collector.Requests.Count} (+{collector.RuntimeRequests.Count} looked up at runtime)");
                var recordedMappings = ReadDuckTypeMaps(options.DuckTypeMaps, report);
                if (collector.Requests.Count + collector.RuntimeRequests.Count + recordedMappings.Count > 0)
                {
                    var registryName = $"Datadog.Trace.DuckType.AotRegistry.{Path.GetFileNameWithoutExtension(options.Assemblies[0])}";
                    var assemblyPaths = modules.Where(m => !StringUtil.IsNullOrEmpty(m.Path))
                                               .GroupBy(m => m.AssemblyName, StringComparer.OrdinalIgnoreCase)
                                               .ToDictionary(g => g.Key, g => Path.GetFullPath(g.First().Path), StringComparer.OrdinalIgnoreCase);
                    if (compositeInterfaces is not null)
                    {
                        assemblyPaths[CompositeInterfaceAssembly.AssemblyName] = compositeInterfaces;
                    }

                    duckTypeRegistry = CallTargetDuckTypeRegistry.Build(collector.Requests, collector.RuntimeRequests, recordedMappings, assemblyPaths, options.OutputDirectory, registryName, options.DatadogTracePath, typeResolver.Resolve);
                    if (duckTypeRegistry is not null)
                    {
                        report.DuckTypeRegistry = new AotInstrumentReport.DuckTypeRegistryResult { Path = duckTypeRegistry.AssemblyPath, Mappings = duckTypeRegistry.Mappings, Compatible = duckTypeRegistry.Compatible, Warnings = duckTypeRegistry.Warnings.ToList() };
                        AotLog.Info($"DuckType AOT registry: {duckTypeRegistry.Compatible}/{duckTypeRegistry.Mappings} proxies -> {duckTypeRegistry.AssemblyPath}");
                    }
                }
            }

            foreach (var module in writableModules)
            {
                var output = Path.Combine(options.OutputDirectory, Path.GetFileName(module.Path));
                var rewritten = rewrittenByModule[module];
                var rewrittenMethods = rewritten.Select(DescribeForVerification).ToList();
                CallTargetRegistryResult? registry = null;
                if (options.GenerateCallTargetRegistry)
                {
                    // The PDB isn't there at runtime: its SourceLink document gives the git metadata.
                    var sourceLink = module == application && module.Module.CustomDebugInfos.OfType<PdbSourceLinkCustomDebugInfo>().FirstOrDefault() is { } link
                                         ? Encoding.UTF8.GetString(link.FileBlob)
                                         : null;
                    registry = CallTargetRegistryGenerator.Generate(module.Module, rewritten, datadogTrace, typeResolver.Resolve, duckTypeRegistry, instantiations, isApplication: module == application, userStrings, sourceLink, codeOriginLocations);
                    AotLog.Info($"{module.AssemblyName}: {registry.Registrations} CallTarget registrations ({registry.Bound} bound, {registry.NoMethod} without integration method, {registry.Failures} failures, {registry.Deferred} deferred, {registry.ContinuationFactories} continuation factories); {registry.Instantiations} instantiations of deferred generic shapes ({registry.InstantiationBound} bound, {registry.InstantiationDeferred} still deferred)");
                    foreach (var detail in registry.Details)
                    {
                        AotLog.Debug(detail);
                    }
                }

                MethodBodies.Neutralize(module, options.Neutralize);
                MethodBodies.Save(module, output);
                if (options.Verify && rewrittenMethods.Count > 0)
                {
                    var outcome = new VerificationsRunner(output, module.Path, rewrittenMethods, options.ReferenceDirectories.Concat(new[] { Path.GetDirectoryName(module.Path)!, Path.GetFullPath(options.OutputDirectory) }).ToList(), failOnVerificationError: true).Run();
                    if (!outcome.IsValid)
                    {
                        report.Errors.Add($"Verification failed for {module.AssemblyName}: {outcome.FailureReason}");
                    }
                }

                report.Assemblies.Add(new AotInstrumentReport.AssemblyResult { Name = module.AssemblyName, Path = output, RewrittenMethods = module.NewBodies.Count, CallTarget = registry });
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

    /// <summary>
    /// The mappings of the ducktype-aot map files recorded at runtime (C6).
    /// </summary>
    private static List<DuckTypeAot.DuckTypeAotMapping> ReadDuckTypeMaps(IReadOnlyList<string> paths, AotInstrumentReport report)
    {
        var mappings = new List<DuckTypeAot.DuckTypeAotMapping>();
        foreach (var path in paths)
        {
            var result = DuckTypeAot.DuckTypeAotMapFileParser.Parse(path);
            report.Errors.AddRange(result.Errors.Select(e => $"{path}: {e}"));
            mappings.AddRange(result.Mappings);
            AotLog.Info($"{path}: {result.Mappings.Count} recorded duck typing mappings");
        }

        return mappings;
    }
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
