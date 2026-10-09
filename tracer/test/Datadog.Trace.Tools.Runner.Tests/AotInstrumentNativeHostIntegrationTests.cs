// <copyright file="AotInstrumentNativeHostIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Tools.Runner.Aot;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Instruments CallTargetNativeTest offline with the native tracer (dd-trace aot instrument) and runs it without a
/// profiler: the CallTarget calls must happen exactly as in the CallTargetNativeTests integration tests, which run the
/// same application with the profiler attached.
/// </summary>
/// <remarks>
/// Opt-in: set DD_AOT_NATIVE_TRACER (Datadog.Tracer.Native library for this host) and DD_AOT_CALLTARGET_NATIVE_TEST_DIR
/// (CallTargetNativeTest build output for net8.0).
/// </remarks>
public class AotInstrumentNativeHostIntegrationTests
{
    private static readonly string[] Modes = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "withref", "without", "withrefstruct", "abstract", "interface", "extras", "calltargetbubbleupexceptions", "instrumentationexceptions"];

    private readonly ITestOutputHelper _output;

    public AotInstrumentNativeHostIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    public void CallTargetNativeTestBehavesAsWithTheProfiler()
    {
        var nativeTracer = Environment.GetEnvironmentVariable("DD_AOT_NATIVE_TRACER");
        var appDirectory = Environment.GetEnvironmentVariable("DD_AOT_CALLTARGET_NATIVE_TEST_DIR");
        Skip.If(string.IsNullOrEmpty(nativeTracer) || string.IsNullOrEmpty(appDirectory), "DD_AOT_NATIVE_TRACER and DD_AOT_CALLTARGET_NATIVE_TEST_DIR are required");

        var workDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-native-host", Guid.NewGuid().ToString("N"));
        try
        {
            // 1. The native rewrite alone: IntegrationMapper and dynamic duck typing run, and record the duck typing mappings the
            //    application creates at runtime, among them the ones looked up by the runtime type of a value (C6).
            var (recordingApp, _) = Instrument(nativeTracer!, appDirectory!, workDirectory, "recording", verify: true, "--no-calltarget-registry");
            var map = Path.Combine(workDirectory, "ducktype-map.json");
            foreach (var mode in Modes)
            {
                var (recordingExit, recordingOutput) = Run(recordingApp, mode, ("DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH", map));
                recordingExit.Should().Be(0, recordingOutput);
            }

            // 2. With the registrations and the recorded mappings: every shape is bound (the generic ones through their closed
            //    instantiations) with the proxies of a DuckType AOT registry, which enables the AOT mode of duck typing: nothing
            //    falls back to IntegrationMapper or creates a proxy dynamically.
            var (app, report) = Instrument(nativeTracer!, appDirectory!, workDirectory, "aot", verify: true, "--ducktype-map", map);
            var callTarget = report["Assemblies"]![0]!["CallTarget"]!;
            callTarget.Value<int>("Failures").Should().Be(0, callTarget.ToString());
            callTarget.Value<int>("Bound").Should().BeGreaterThan(0, callTarget.ToString());
            callTarget.Value<int>("InstantiationDeferred").Should().Be(0, callTarget.ToString());
            var duckTypeRegistry = report["DuckTypeRegistry"]!;
            duckTypeRegistry.Value<int>("Compatible").Should().Be(duckTypeRegistry.Value<int>("Mappings"), duckTypeRegistry.ToString());

            for (var arguments = 0; arguments < 10; arguments++)
            {
                var (appExit, appOutput) = Run(app, arguments.ToString());
                appExit.Should().Be(0, appOutput);
                var begin = arguments < 9 ? $"ProfilerOK: BeginMethod\\({arguments}\\)" : "ProfilerOK: BeginMethod\\(Array\\)";
                var (expectedCalls, expectedExceptions) = arguments switch
                {
                    0 => (240, 44),
                    1 => (235, 40),
                    _ => (228, 40),
                };
                Regex.Matches(appOutput, begin).Count.Should().Be(expectedCalls, $"arguments: {arguments}");
                Regex.Matches(appOutput, "ProfilerOK: EndMethod\\(").Count.Should().Be(expectedCalls, $"arguments: {arguments}");
                Regex.Matches(appOutput, "Exception thrown.").Count.Should().Be(expectedExceptions, $"arguments: {arguments}");
            }

            var (refExit, refOutput) = Run(app, "withref");
            refExit.Should().Be(0, refOutput);
            Regex.Matches(refOutput, "ProfilerOK: BeginMethod\\(1\\)").Count.Should().Be(9);
            Regex.Matches(refOutput, "ProfilerOK: BeginMethod\\(2\\)").Count.Should().Be(8);
            Regex.Matches(refOutput, "ProfilerOK: EndMethod\\(").Count.Should().Be(17);

            foreach (var mode in new[] { "without", "withrefstruct", "abstract", "interface", "extras", "calltargetbubbleupexceptions", "instrumentationexceptions" })
            {
                var (modeExit, modeOutput) = Run(app, mode);
                modeExit.Should().Be(0, modeOutput);
                modeOutput.Should().Contain("ProfilerOK", mode);

                // Like IntegrationMapper's dynamic methods, the adapters don't show in stack traces.
                modeOutput.Should().NotContain(CallTargetRegistryGenerator.RegistrationTypePrefix, mode);
            }
        }
        finally
        {
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
        }
    }

    [SkippableFact]
    public void TraceMethodsAreInstrumentedAtBuildTime()
    {
        var nativeTracer = Environment.GetEnvironmentVariable("DD_AOT_NATIVE_TRACER");
        var appDirectory = Environment.GetEnvironmentVariable("DD_AOT_CALLTARGET_NATIVE_TEST_DIR");
        Skip.If(string.IsNullOrEmpty(nativeTracer) || string.IsNullOrEmpty(appDirectory), "DD_AOT_NATIVE_TRACER and DD_AOT_CALLTARGET_NATIVE_TEST_DIR are required");

        var workDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-native-host", Guid.NewGuid().ToString("N"));
        try
        {
            // DD_TRACE_METHODS at build time: the trace annotations integration instruments the method too. Not verified:
            // ILSpy doesn't decompile the ldtoken of the method that integration passes (PrepareMethod and ILVerify accept it).
            var (_, baseline) = Instrument(nativeTracer!, appDirectory!, workDirectory, "baseline", verify: false);
            var (_, report) = Instrument(nativeTracer!, appDirectory!, workDirectory, "tracemethods", verify: false, "--trace-methods", "CallTargetNativeTest.Program[Main]");
            var rewritten = report["Assemblies"]![0]!.Value<int>("RewrittenMethods");
            rewritten.Should().Be(baseline["Assemblies"]![0]!.Value<int>("RewrittenMethods") + 1, report.ToString());
        }
        finally
        {
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// The same inputs give the same outputs (F2.10): incremental builds and build caches can rely on them.
    /// </summary>
    [SkippableFact]
    public void InstrumentationIsDeterministic()
    {
        var nativeTracer = Environment.GetEnvironmentVariable("DD_AOT_NATIVE_TRACER");
        var appDirectory = Environment.GetEnvironmentVariable("DD_AOT_CALLTARGET_NATIVE_TEST_DIR");
        Skip.If(string.IsNullOrEmpty(nativeTracer) || string.IsNullOrEmpty(appDirectory), "DD_AOT_NATIVE_TRACER and DD_AOT_CALLTARGET_NATIVE_TEST_DIR are required");

        var workDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-native-host", Guid.NewGuid().ToString("N"));
        try
        {
            // The same output folder too, like a project's: the instrumented assemblies name their PDB with its path.
            Instrument(nativeTracer!, appDirectory!, workDirectory, "run", verify: false);
            var first = Path.Combine(workDirectory, "first");
            CopyDirectory(Path.Combine(workDirectory, "run", "instrumented"), first);
            Instrument(nativeTracer!, appDirectory!, workDirectory, "run", verify: false);
            var second = Path.Combine(workDirectory, "run", "instrumented");
            // What ILC compiles: the assemblies, their PDBs and the descriptors (the reports have the time they were written).
            var files = Directory.GetFiles(first)
                                 .Select(Path.GetFileName)
                                 .OfType<string>()
                                 .Where(f => Path.GetExtension(f) is ".dll" or ".pdb" or ".xml")
                                 .OrderBy(f => f, StringComparer.Ordinal)
                                 .ToList();
            files.Should().Contain("CallTargetNativeTest.dll").And.Contain(f => f.StartsWith("Datadog.Trace.DuckType.AotRegistry.", StringComparison.Ordinal));
            files.Where(file => !File.ReadAllBytes(Path.Combine(first, file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(second, file))))
                 .Should().BeEmpty("the instrumentation must write the same bytes for the same inputs");
        }
        finally
        {
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// The DD_* settings of the build environment are the application's runtime settings: they don't change what is
    /// instrumented (the native tracer reads them from the environment).
    /// </summary>
    [SkippableFact]
    public void BuildEnvironmentDoesNotChangeTheInstrumentation()
    {
        var nativeTracer = Environment.GetEnvironmentVariable("DD_AOT_NATIVE_TRACER");
        var appDirectory = Environment.GetEnvironmentVariable("DD_AOT_CALLTARGET_NATIVE_TEST_DIR");
        Skip.If(string.IsNullOrEmpty(nativeTracer) || string.IsNullOrEmpty(appDirectory), "DD_AOT_NATIVE_TRACER and DD_AOT_CALLTARGET_NATIVE_TEST_DIR are required");

        var workDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-native-host", Guid.NewGuid().ToString("N"));
        try
        {
            var (_, baseline) = Instrument(nativeTracer!, appDirectory!, workDirectory, "baseline", verify: false, "--trace-methods", "CallTargetNativeTest.Program[Main]");
            var (_, report) = InstrumentWithEnvironment(
                nativeTracer!,
                appDirectory!,
                workDirectory,
                "environment",
                verify: false,
                [("DD_TRACE_ENABLED", "false"), ("DD_TRACE_ANNOTATIONS_ENABLED", "false"), ("DD_DISABLED_INTEGRATIONS", "CallTargetNativeTest")],
                "--trace-methods",
                "CallTargetNativeTest.Program[Main]");

            report["Assemblies"]![0]!.Value<int>("RewrittenMethods").Should().Be(baseline["Assemblies"]![0]!.Value<int>("RewrittenMethods"), report.ToString());
            Directory.GetFiles(Path.Combine(workDirectory, "environment", "instrumented", "logs")).Should().NotBeEmpty("the native tracer logs to the output folder");
        }
        finally
        {
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
        }
    }

    [SkippableFact]
    public void ApplicationGivenTwiceKeepsItsModuleInitializer()
    {
        var nativeTracer = Environment.GetEnvironmentVariable("DD_AOT_NATIVE_TRACER");
        var appDirectory = Environment.GetEnvironmentVariable("DD_AOT_CALLTARGET_NATIVE_TEST_DIR");
        Skip.If(string.IsNullOrEmpty(nativeTracer) || string.IsNullOrEmpty(appDirectory), "DD_AOT_NATIVE_TRACER and DD_AOT_CALLTARGET_NATIVE_TEST_DIR are required");

        var workDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-native-host", Guid.NewGuid().ToString("N"));
        try
        {
            // The .NET 11 SDK also passes the application as a reference of ILC: it is instrumented once, as the application.
            var (_, report) = Instrument(nativeTracer!, appDirectory!, workDirectory, "twice", verify: false, "--assembly", Path.Combine(appDirectory!, "CallTargetNativeTest.dll"));
            report["Assemblies"]!.Count(a => a.Value<string>("Name") == "CallTargetNativeTest").Should().Be(1, report.ToString());

            using var module = ModuleDefMD.Load(Path.Combine(workDirectory, "twice", "instrumented", "CallTargetNativeTest.dll"));
            module.GlobalType.FindStaticConstructor().Should().NotBeNull();
            module.GlobalType.Methods.Should().Contain(m => m.Name == "<DatadogAot>InitializeInstrumentation");
        }
        finally
        {
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination), overwrite: true);
        }
    }

    /// <summary>
    /// Instruments CallTargetNativeTest into its own application folder.
    /// </summary>
    private (string Application, JObject Report) Instrument(string nativeTracer, string appDirectory, string workDirectory, string name, bool verify, params string[] extraArguments)
        => InstrumentWithEnvironment(nativeTracer, appDirectory, workDirectory, name, verify, [], extraArguments);

    private (string Application, JObject Report) InstrumentWithEnvironment(string nativeTracer, string appDirectory, string workDirectory, string name, bool verify, (string Name, string Value)[] environment, params string[] extraArguments)
    {
        var instrumented = Path.Combine(workDirectory, name, "instrumented");
        var app = Path.Combine(workDirectory, name, "app");
        var reportPath = Path.Combine(workDirectory, name, "report.json");
        var appAssembly = Path.Combine(appDirectory, "CallTargetNativeTest.dll");
        var arguments = new List<string>
        {
            typeof(AotInstrumentProcessor).Assembly.Location,
            "aot",
            "instrument",
            "--native-tracer",
            nativeTracer,
            "--datadog-trace",
            Path.Combine(appDirectory, "Datadog.Trace.dll"),
            "--assembly",
            appAssembly,
            "--reference-dir",
            RuntimeEnvironment.GetRuntimeDirectory(),
            "--runtime-version",
            Environment.Version.ToString(3),
            "--no-embedded-definitions",
            "--definitions-assembly",
            appAssembly,
            "--definitions-method",
            "CallTargetNativeTest.Program::InjectCallTargetDefinitions",
            "--neutralize",
            "CallTargetNativeTest.Program::InjectCallTargetDefinitions",
            "--report",
            reportPath,
            "--output",
            instrumented,
        };
        if (verify)
        {
            arguments.Add("--verify");
        }

        arguments.AddRange(extraArguments);
        var (exitCode, output) = RunProcess("dotnet", appDirectory, arguments, environment);
        exitCode.Should().Be(0, output);

        CopyDirectory(appDirectory, app);
        foreach (var file in Directory.GetFiles(instrumented).Where(f => f.EndsWith(".dll", StringComparison.Ordinal) || f.EndsWith(".pdb", StringComparison.Ordinal)))
        {
            File.Copy(file, Path.Combine(app, Path.GetFileName(file)), overwrite: true);
        }

        // The DuckType AOT registry isn't in the dependency manifest: without it, the host probes the application folder.
        File.Delete(Path.Combine(app, "CallTargetNativeTest.deps.json"));
        return (app, JObject.Parse(File.ReadAllText(reportPath)));
    }

    private (int ExitCode, string Output) Run(string application, string mode, params (string Name, string Value)[] environment)
        => RunProcess("dotnet", application, ["CallTargetNativeTest.dll", mode], environment);

    private (int ExitCode, string Output) RunProcess(string fileName, string workingDirectory, IEnumerable<string> arguments, (string Name, string Value)[] environment)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // The application must run without the profiler: everything comes from the offline rewrite.
        startInfo.Environment.Remove("CORECLR_ENABLE_PROFILING");
        startInfo.Environment["DD_TRACE_AGENT_URL"] = "http://127.0.0.1:1";
        startInfo.Environment["DD_TELEMETRY_ENABLED"] = "0";
        startInfo.Environment["DD_REMOTE_CONFIGURATION_ENABLED"] = "0";
        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit(300_000).Should().BeTrue($"{fileName} {string.Join(" ", startInfo.ArgumentList)} timed out");
        var output = standardOutput.Result + Environment.NewLine + standardError.Result;
        if (process.ExitCode != 0)
        {
            _output.WriteLine(output);
        }

        return (process.ExitCode, output);
    }
}
#endif
