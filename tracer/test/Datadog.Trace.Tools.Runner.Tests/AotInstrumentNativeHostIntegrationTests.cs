// <copyright file="AotInstrumentNativeHostIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Tools.Runner.Aot;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
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
        var instrumented = Path.Combine(workDirectory, "instrumented");
        var app = Path.Combine(workDirectory, "app");
        try
        {
            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            var appAssembly = Path.Combine(appDirectory!, "CallTargetNativeTest.dll");
            var runner = typeof(AotInstrumentProcessor).Assembly.Location;
            var (exitCode, output) = Run(
                "dotnet",
                appDirectory!,
                runner,
                "aot",
                "instrument",
                "--native-tracer",
                nativeTracer!,
                "--datadog-trace",
                Path.Combine(appDirectory!, "Datadog.Trace.dll"),
                "--assembly",
                appAssembly,
                "--reference-dir",
                runtimeDirectory,
                "--runtime-version",
                Environment.Version.ToString(3),
                "--no-embedded-definitions",
                "--definitions-assembly",
                appAssembly,
                "--definitions-method",
                "CallTargetNativeTest.Program::InjectCallTargetDefinitions",
                "--neutralize",
                "CallTargetNativeTest.Program::InjectCallTargetDefinitions",
                "--verify",
                "--report",
                Path.Combine(workDirectory, "report.json"),
                "--output",
                instrumented);
            exitCode.Should().Be(0, output);

            // The registrations bind every shape without duck typing; the rest falls back to IntegrationMapper (F5).
            var report = JObject.Parse(File.ReadAllText(Path.Combine(workDirectory, "report.json")));
            var callTarget = report["Assemblies"]![0]!["CallTarget"]!;
            callTarget.Value<int>("Failures").Should().Be(0, callTarget.ToString());
            callTarget.Value<int>("Bound").Should().BeGreaterThan(0, callTarget.ToString());

            CopyDirectory(appDirectory!, app);
            File.Copy(Path.Combine(instrumented, "CallTargetNativeTest.dll"), Path.Combine(app, "CallTargetNativeTest.dll"), overwrite: true);
            File.Copy(Path.Combine(instrumented, "CallTargetNativeTest.pdb"), Path.Combine(app, "CallTargetNativeTest.pdb"), overwrite: true);

            for (var arguments = 0; arguments < 10; arguments++)
            {
                var (appExit, appOutput) = Run("dotnet", app, "CallTargetNativeTest.dll", arguments.ToString());
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

            var (refExit, refOutput) = Run("dotnet", app, "CallTargetNativeTest.dll", "withref");
            refExit.Should().Be(0, refOutput);
            Regex.Matches(refOutput, "ProfilerOK: BeginMethod\\(1\\)").Count.Should().Be(9);
            Regex.Matches(refOutput, "ProfilerOK: BeginMethod\\(2\\)").Count.Should().Be(8);
            Regex.Matches(refOutput, "ProfilerOK: EndMethod\\(").Count.Should().Be(17);

            foreach (var mode in new[] { "without", "withrefstruct", "abstract", "interface", "extras", "calltargetbubbleupexceptions", "instrumentationexceptions" })
            {
                var (modeExit, modeOutput) = Run("dotnet", app, "CallTargetNativeTest.dll", mode);
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

    private (int ExitCode, string Output) Run(string fileName, string workingDirectory, params string[] arguments)
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

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit(300_000).Should().BeTrue($"{fileName} {string.Join(" ", arguments)} timed out");
        var output = standardOutput.Result + Environment.NewLine + standardError.Result;
        if (process.ExitCode != 0)
        {
            _output.WriteLine(output);
        }

        return (process.ExitCode, output);
    }
}
#endif
