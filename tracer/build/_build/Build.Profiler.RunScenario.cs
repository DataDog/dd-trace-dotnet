using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using static Nuke.Common.EnvironmentInfo;
using Logger = Serilog.Log;

partial class Build
{
    // Start with the ID: Nuke treats values beginning with '--' as build parameters.
    [Parameter("Profiler scenario ID followed by sample options. Default: 4 (PiComputation, 30 seconds).")]
    readonly string Scenario = "4";

    Target RunProfilerScenario => _ => _
        .Description("Run an already-built Samples.Computer01 scenario on Linux. Logs and profiles go to artifacts/build_data/logs/profiler_scenario/.")
        .Requires(() => IsLinux)
        .After(BuildProfilerHome, BuildProfilerSamples, BuildTracerHome, BuildNativeLoader, BuildNativeWrapper)
        .Executes(() =>
        {
            if (!string.IsNullOrEmpty(SampleName) && SampleName != "Samples.Computer01")
            {
                throw new ArgumentException("RunProfilerScenario only supports Samples.Computer01. Omit --sample-name or use --sample-name Samples.Computer01.");
            }

            var sample = ProfilerSamplesSolution.GetProject("Samples.Computer01");
            var framework = Framework ?? TargetFramework.NET11_0;
            var sampleDir = ProfilerOutputDirectory / "bin" / $"{BuildConfiguration}-{TargetPlatform}" / "profiler" / "src" / "Demos" / sample.Name / framework;
            var sampleDll = sampleDir / $"{sample.Name}.dll";
            var (arch, ext) = GetUnixArchitectureAndExtension();
            var home = MonitoringHomeDirectory / arch;
            var requiredFiles = new[]
            {
                $"{FileNames.NativeLoader}.{ext}",
                $"{FileNames.NativeProfiler}.{ext}",
                $"{FileNames.NativeTracer}.{ext}",
                FileNames.ProfilerLinuxApiWrapper,
                FileNames.LoaderConf,
            };
            foreach (var file in requiredFiles)
            {
                if (!File.Exists(home / file))
                {
                    throw new Exception(
                        $"Missing {home / file}. For a cold checkout run BuildTracerHome BuildNativeWrapper BuildProfilerHome BuildProfilerSamples first.");
                }
            }

            var logsDir = TestLogsDirectory / "profiler_scenario" / sample.Name;
            var pprofDir = logsDir / "pprof";
            var args = TokenizeShellArgs(Scenario).ToList();
            args.Insert(0, "--scenario");
            if (!args.Contains("--timeout", StringComparer.OrdinalIgnoreCase)
                && !args.Contains("--iterations", StringComparer.OrdinalIgnoreCase)
                && !args.Contains("--run-infinitely", StringComparer.OrdinalIgnoreCase))
            {
                args.AddRange(new[] { "--timeout", "30" });
            }

            var sampleArgs = string.Join(" ", args.Select(EscapeArgForCommandLine));
            Logger.Information("Running {Sample} ({Configuration}, {Framework}) on {Arch}: {Args}", sample.Name, BuildConfiguration, framework, arch, sampleArgs);
            RunSampleWithProfiler(TargetPlatform, sampleDll, sampleArgs, logsDir, pprofDir, new Dictionary<string, string>(), ExtraEnvVars);
            AssertProfilerOutputs(logsDir, pprofDir);
        });
}
