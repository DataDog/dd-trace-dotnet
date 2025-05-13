using System.Linq;
using Nuke.Common;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Logger = Serilog.Log;
// #pragma warning disable SA1306
// #pragma warning disable SA1134
// #pragma warning disable SA1111
// #pragma warning disable SA1400
// #pragma warning disable SA1401

partial class Build
{
    [Parameter("Specifies the type of debugging information that should be included in the compiled assembly. Used for debugger integrations tests", List = false)]
    readonly string DebugType;

    [Parameter("Optimize generated code. Used for debugger integrations tests", List = false)]
    readonly bool? Optimize;

    TargetFramework[] TestingFrameworksDebugger =>
        TargetFramework.GetFrameworks(except: new[] { TargetFramework.NET461, TargetFramework.NETSTANDARD2_0, TargetFramework.NETCOREAPP3_0, TargetFramework.NET5_0 });

    Project DebuggerIntegrationTests => Solution.GetProject(Projects.DebuggerIntegrationTests);

    Project DebuggerSamples => Solution.GetProject(Projects.DebuggerSamples);

    Project ExceptionReplaySamples => Solution.GetProject(Projects.ExceptionReplaySamples);

    Project DebuggerSamplesTestRuns => Solution.GetProject(Projects.DebuggerSamplesTestRuns);

    Project DebuggerUnreferencedExternal => Solution.GetProject(Projects.DebuggerUnreferencedExternal);

    Target BuildAndRunDebuggerIntegrationTests => _ => _
        .Description("Builds and runs the debugger integration tests")
        .DependsOn(BuildDebuggerIntegrationTests)
        .DependsOn(RunDebuggerIntegrationTests);

    Target BuildDebuggerIntegrationTests => _ => _
        .Unlisted()
        .Description("Builds the debugger integration tests")
        .DependsOn(CompileDebuggerIntegrationTests);

    Target CompileDebuggerIntegrationTests => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CompileDebuggerIntegrationTestsDependencies)
        .DependsOn(CompileDebuggerIntegrationTestsSamples)
        .Requires(() => Framework)
        .Requires(() => MonitoringHomeDirectory != null)
        .Executes(() =>
        {
            DotnetBuild(DebuggerIntegrationTests, framework: Framework);
        });

    Target CompileDebuggerIntegrationTestsDependencies => _ => _
        .Unlisted()
        .Requires(() => Framework)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Optimize != null)
        .Requires(() => DebugType != null)
        .Executes(() =>
        {
            DotnetBuild(DebuggerUnreferencedExternal, framework: Framework, noDependencies: false);
            DotnetBuild(DebuggerSamplesTestRuns, framework: Framework, noDependencies: false);
        });

    Target CompileDebuggerIntegrationTestsSamples => _ => _
        .Unlisted()
        .DependsOn(HackForMissingMsBuildLocation)
        .DependsOn(CompileDebuggerIntegrationTestsDependencies)
        .Requires(() => Framework)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Optimize != null)
        .Requires(() => DebugType != null)
        .Executes(() =>
        {
            DotnetBuild(DebuggerSamples, framework: Framework);

            if (ExceptionReplaySamples.TryGetTargetFrameworks().Contains(Framework))
            {
                DotnetBuild(ExceptionReplaySamples, framework: Framework);
            }

            if (!IsWin)
            {
                // The sample helper in the test library assumes that the sample has
                // been published when running on Linux
                DotNetPublish(x => x
                    .SetFramework(Framework)
                    .SetConfiguration(BuildConfiguration)
                    .SetNoWarnDotNetCore3()
                    .SetProject(DebuggerSamples));

                if (ExceptionReplaySamples.TryGetTargetFrameworks().Contains(Framework))
                {
                    DotNetPublish(x => x
                                      .SetFramework(Framework)
                                      .SetConfiguration(BuildConfiguration)
                                      .SetNoWarnDotNetCore3()
                                      .SetProject(ExceptionReplaySamples));
                }
            }
        });

    Target RunDebuggerIntegrationTests => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .After(BuildDebuggerIntegrationTests)
        .Requires(() => Framework)
        .Triggers(PrintSnapshotsDiff)
        .Executes(() =>
        {
            var isDebugRun = IsDebugRun();
            EnsureCleanDirectory(TestLogsDirectory);
            EnsureResultsDirectory(DebuggerIntegrationTests);

            try
            {
                DotNetTest(config => config
                    .SetDotnetPath(TargetPlatform)
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatformAnyCPU()
                    .SetFramework(Framework)
                    .EnableCrashDumps()
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(GetTestFilter())
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetIsDebugRun(isDebugRun)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverageEnabled, ConfigureCodeCoverage)
                    .EnableTrxLogOutput(GetResultsDirectory(DebuggerIntegrationTests))
                    .WithDatadogLogger()
                    .SetProjectFile(DebuggerIntegrationTests));

                string GetTestFilter()
                {
                    var filter = (IsWin, IsArm64) switch
                    {
                        (true, _) => "(RunOnWindows=True)&(SkipInCI!=True)",
                        (_, true) => "(Category!=ArmUnsupported)&(SkipInCI!=True)",
                        _ => "(Category!=LinuxUnsupported)&(SkipInCI!=True)",
                    };

                    return Filter is null ? filter : $"{Filter}&{filter}";
                }
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });
}
