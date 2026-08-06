using System;
using System.Collections.Generic;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using Logger = Serilog.Log;

// Developer-facing target to run a Samples.Computer01 scenario with the locally-built
// profiler attached. Linux-only (the profiler runtime is Linux-musl); from macOS / Windows
// hosts, invoke via tracer/build_in_docker.{sh,ps1}. See AGENTS.md for the full loop.

partial class Build
{
    [Parameter("Profiler scenario ID to run (Scenario enum value in Samples.Computer01/Program.cs). Default: 4 = PiComputation.")]
    readonly int Scenario = 4;

    [Parameter("Scenario timeout, in seconds. Default: 30.")]
    readonly int ScenarioTimeout = 30;

    [Parameter("Extra args appended after --scenario / --timeout (e.g. \"--param 1\").")]
    readonly string ScenarioArgs = string.Empty;

    Target RunProfilerScenario => _ => _
        .Description("Run a Samples.Computer01 scenario with the locally-built profiler attached. Outputs to .profiler-out/.")
        .OnlyWhenStatic(() => IsLinux)
        .After(BuildProfilerHome, BuildProfilerSamples, BuildTracerHome, BuildNativeLoader, BuildNativeWrapper)
        .Executes(() =>
        {
            var (arch, ext) = GetUnixArchitectureAndExtension();
            var mhArch = MonitoringHomeDirectory / arch;

            // Sanity-check the monitoring home — fail fast with a clear hint if a Build*Home target was skipped.
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
                var path = mhArch / file;
                if (!File.Exists(path))
                {
                    throw new Exception(
                        $"Missing {path}. Run the relevant Build*Home / BuildNative* target first. " +
                        "For a cold checkout: BuildTracerHome BuildNativeLoader BuildNativeWrapper BuildProfilerHome BuildProfilerSamples.");
                }
            }

            // Locate Samples.Computer01.dll for the current platform / configuration.
            // Prefer Release; fall back to Debug if a Debug-only build is present.
            var sampleRel = ProfilerOutputDirectory / "bin" / $"{Configuration.Release}-{TargetPlatform}" / "profiler" / "src" / "Demos" / "Samples.Computer01" / "net10.0";
            var sampleDir = File.Exists(sampleRel / "Samples.Computer01.dll")
                ? sampleRel
                : ProfilerOutputDirectory / "bin" / $"{Configuration.Debug}-{TargetPlatform}" / "profiler" / "src" / "Demos" / "Samples.Computer01" / "net10.0";

            var sampleDll = sampleDir / "Samples.Computer01.dll";
            if (!File.Exists(sampleDll))
            {
                throw new Exception($"Sample not found under {sampleRel} or Debug equivalent. Run BuildProfilerSamples first.");
            }

            // Output directories (gitignored).
            var outDir = RootDirectory / ".profiler-out";
            var logsDir = outDir / "logs";
            var pprofDir = outDir / "pprof";
            EnsureExistingDirectory(logsDir);
            EnsureExistingDirectory(pprofDir);

            var envVars = new Dictionary<string, string>();
            AddContinuousProfilerEnvironmentVariables(envVars);
            envVars["DD_NATIVELOADER_CONFIGFILE"] = mhArch / FileNames.LoaderConf;
            envVars["LD_PRELOAD"] = mhArch / FileNames.ProfilerLinuxApiWrapper;
            envVars["DD_PROFILING_ENABLED"] = "1";
            envVars["DD_PROFILING_MANAGED_ACTIVATION_ENABLED"] = "0";
            envVars["DD_TRACE_ENABLED"] = "0";
            envVars["DD_TRACE_LOG_DIRECTORY"] = logsDir;
            envVars["DD_INTERNAL_PROFILING_OUTPUT_DIR"] = pprofDir;
            if (IsArm64)
            {
                // Profiler is opt-in on arm64 via this internal flag.
                envVars["DD_INTERNAL_PROFILING_ENABLED_ARM64"] = "1";
            }

            var args = $"{sampleDll} --scenario {Scenario} --timeout {ScenarioTimeout}";
            if (!string.IsNullOrWhiteSpace(ScenarioArgs))
            {
                args += " " + ScenarioArgs;
            }

            Logger.Information("Running scenario {Scenario} (timeout {Timeout}s) on {Arch}.", Scenario, ScenarioTimeout, arch);
            Logger.Information("Logs:   {Logs}", logsDir);
            Logger.Information("Pprofs: {Pprofs}", pprofDir);

            var dotnetPath = DotNetSettingsExtensions.GetDotNetPath(TargetPlatform);
            using var process = ProcessTasks.StartProcess(
                toolPath: dotnetPath,
                arguments: args,
                workingDirectory: sampleDir,
                environmentVariables: envVars,
                customLogger: DotNetTasks.DotNetLogger);
            process.AssertZeroExitCode();

            // Post-run sanity check: surface profiler-load failures clearly. Without this the only
            // symptom is "no .pprof appeared".
            var loaderLog = logsDir / "dotnet-native-loader-dotnet-1.log";
            if (File.Exists(loaderLog))
            {
                var content = File.ReadAllText(loaderLog);
                if (content.Contains("Error loading dynamic library") && content.Contains(FileNames.NativeProfiler))
                {
                    Logger.Warning("The native profiler library failed to load — no .pprof will be produced.");
                    Logger.Warning("Hint: rebuild the profiler home (BuildProfilerHome).");
                }
            }
        });
}
