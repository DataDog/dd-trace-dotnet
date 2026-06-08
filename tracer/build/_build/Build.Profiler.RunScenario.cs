using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.MSBuild;
using static Nuke.Common.EnvironmentInfo;

// Developer-facing iteration target for the Linux profiler. See AGENTS.md.

partial class Build
{
    [Parameter("Profiler scenario ID to run (Scenario enum value in Samples.Computer01/Program.cs). Default: 4 = PiComputation.")]
    readonly int Scenario = 4;

    [Parameter("Scenario timeout, in seconds. Default: 30.")]
    readonly int ScenarioTimeout = 30;

    [Parameter("Scenario parameter forwarded as --param N to the sample (used by GarbageCollection, MemoryLeak, ContentionGenerator, etc.).")]
    readonly int? ScenarioParam;

    [Parameter("Extra args appended verbatim after --scenario / --timeout (one Nuke arg = one sample arg). Values starting with '--' should be prefixed with a space to bypass Nuke's flag detection, e.g. --scenario-args \" --iterations\" 100.")]
    readonly string[] ScenarioArgs;

    AbsolutePath ProfilerScenarioOutputDirectory => BuildDataDirectory / "profiler-scenario";

    Target RunProfilerScenario => _ => _
        .Description("Run a Samples.Computer01 scenario with the locally-built profiler attached. Outputs to artifacts/build_data/profiler-scenario/.")
        .OnlyWhenStatic(() => IsLinux)
        .After(BuildProfilerHome, BuildProfilerSamples, BuildTracerHome, BuildNativeLoader, BuildNativeWrapper)
        .Executes(() =>
        {
            var args = string.Empty;
            if (ScenarioParam is { } p)
            {
                args = $"--param {p}";
            }
            if (ScenarioArgs is { Length: > 0 })
            {
                // Trim the per-arg leading space used to bypass Nuke's --flag detection.
                var joined = string.Join(" ", ScenarioArgs.Select(a => a.TrimStart()));
                args = string.IsNullOrEmpty(args) ? joined : $"{args} {joined}";
            }

            // Configuration is left null so the helper resolves it (BuildConfiguration first, with a
            // fallback to the opposite if only that one is locally built). Framework defaults to the
            // latest declared TargetFramework so the dev loop doesn't need bumping at each .NET release.
            RunSampleWithProfiler(
                platform: IsArm64 ? ARM64TargetPlatform : MSBuildTargetPlatform.x64,
                framework: Framework ?? TargetFramework.GetFrameworks().Last(),
                scenario: Scenario,
                timeoutSeconds: ScenarioTimeout,
                outputBaseDir: ProfilerScenarioOutputDirectory,
                extraArgs: args);

            AssertProfilerOutputs(ProfilerScenarioOutputDirectory);
        });
}
