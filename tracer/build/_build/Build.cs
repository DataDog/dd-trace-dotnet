using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

// #pragma warning disable SA1306
// #pragma warning disable SA1134
// #pragma warning disable SA1111
// #pragma warning disable SA1400
// #pragma warning disable SA1401

[ShutdownDotNetAfterServerBuild]
partial class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration BuildConfiguration = Configuration.Release;

    [Parameter("Platform to build - x86 or x64. Default is x64")]
    readonly MSBuildTargetPlatform TargetPlatform = MSBuildTargetPlatform.x64;

    [Parameter("The TargetFramework to execute when running or building a sample app, or linux integration tests")]
    readonly TargetFramework Framework;

    [Parameter("Should all versions of integration NuGet packages be tested")]
    readonly bool TestAllPackageVersions;

    [Parameter("Should minor versions of integration NuGet packages be included")]
    readonly bool IncludeMinorPackageVersions;

    [Parameter("The location to create the monitoring home directory. Default is ./shared/bin/monitoring-home ")]
    readonly AbsolutePath MonitoringHome;
    [Parameter("The location to create the tracer home directory. Default is ./shared/bin/monitoring-home/tracer ")]
    readonly AbsolutePath TracerHome;
    [Parameter("The location to create the dd-trace home directory. Default is ./bin/dd-tracer-home ")]
    readonly AbsolutePath DDTracerHome;
    [Parameter("The location to place NuGet packages and other packages. Default is ./bin/artifacts ")]
    readonly AbsolutePath Artifacts;
    [Parameter("An optional suffix for the beta profiler-tracer MSI. Default is '' ")]
    readonly string BetaMsiSuffix = string.Empty;

    [Parameter("The location to the find the profiler build artifacts. Default is ./profiler/_build/DDProf-Deploy")]
    readonly AbsolutePath ProfilerHome;

    [Parameter("The location to restore Nuget packages (optional) ")]
    readonly AbsolutePath NugetPackageDirectory;

    [Parameter("Is the build running on Alpine linux? Default is 'false'")]
    readonly bool IsAlpine = false;

    [Parameter("The build version. Default is latest")]
    readonly string Version = "2.8.0";

    [Parameter("Whether the build version is a prerelease(for packaging purposes). Default is latest")]
    readonly bool IsPrerelease = false;

    [Parameter("Prints the available drive space before executing each target. Defaults to false")]
    readonly bool PrintDriveSpace = false;

    [Parameter("Override the default test filters for integration tests. (Optional)")]
    readonly string Filter;

    [Parameter("Enables code coverage")]
    readonly bool CodeCoverage;

    [Parameter("The directory containing the tool .nupkg file")]
    readonly AbsolutePath ToolSource;

    [Parameter("The directory to install the tool to")]
    readonly AbsolutePath ToolDestination;

    Target Info => _ => _
        .Description("Describes the current configuration")
        .Before(Clean, Restore, BuildTracerHome)
        .Executes(() =>
        {
            Logger.Info($"Configuration: {BuildConfiguration}");
            Logger.Info($"Platform: {TargetPlatform}");
            Logger.Info($"Framework: {Framework}");
            Logger.Info($"TestAllPackageVersions: {TestAllPackageVersions}");
            Logger.Info($"TracerHomeDirectory: {TracerHomeDirectory}");
            Logger.Info($"ArtifactsDirectory: {ArtifactsDirectory}");
            Logger.Info($"NugetPackageDirectory: {NugetPackageDirectory}");
            Logger.Info($"IsAlpine: {IsAlpine}");
            Logger.Info($"Version: {Version}");
        });

    Target Clean => _ => _
        .Description("Cleans all build output")
        .Executes(() =>
        {
            if (IsWin)
            {
                // These are created as part of the CreatePlatformlessSymlinks target and cause havok
                // when deleting directories otherwise
                DeleteReparsePoints(SourceDirectory);
                DeleteReparsePoints(TestsDirectory);
            }
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => DeleteDirectory(x));
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => DeleteDirectory(x));
            DistributionHomeDirectory.GlobFiles("**").Where(x => !x.ToString().Contains("readme.txt")).ForEach(x => DeleteFile(x));
            EnsureCleanDirectory(OutputDirectory);
            EnsureCleanDirectory(TracerHomeDirectory);
            EnsureCleanDirectory(DDTracerHomeDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(NativeProfilerProject.Directory / "build");
            EnsureCleanDirectory(NativeProfilerProject.Directory / "deps");
            EnsureCleanDirectory(BuildDataDirectory);
            EnsureCleanDirectory(ExplorationTestsDirectory);
            DeleteFile(WindowsTracerHomeZip);

            void DeleteReparsePoints(string path)
            {
                new DirectoryInfo(path)
                   .GetDirectories("*", SearchOption.AllDirectories)
                   .Where(x => x.Attributes.HasFlag(FileAttributes.ReparsePoint))
                   .ForEach(dir => Cmd.Value(arguments: $"cmd /c rmdir \"{dir}\""));
            }
        });

    Target CleanObjFiles => _ => _
         .Unlisted()
         .Description("Deletes all build output files, but preserves folders to work around AzDo issues")
         .Executes(() =>
          {
              TestsDirectory.GlobFiles("**/bin/*", "**/obj/*").ForEach(DeleteFile);
          });

    Target BuildTracerHome => _ => _
        .Description("Builds the native and managed src, and publishes the tracer home directory")
        .After(Clean)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(Restore)
        .DependsOn(CompileManagedSrc)
        .DependsOn(PublishManagedProfiler)
        .DependsOn(CompileNativeSrc)
        .DependsOn(PublishNativeProfiler)
        .DependsOn(DownloadLibDdwaf)
        .DependsOn(CopyLibDdwaf)
        .DependsOn(CreateDdTracerHome);

    Target BuildProfilerHome => _ => _
        .Description("Builds the Profiler native and managed src, and publishes the profiler home directory")
        .After(Clean)
        .DependsOn(CompileProfilerManagedSrc)
        .DependsOn(CompileProfilerNativeSrc);

    Target BuildNativeLoader => _ => _
        .Description("Builds the Native Loader, and publishes to the monitoring home directory")
        .After(Clean)
        .DependsOn(CompileNativeLoader)
        .DependsOn(PublishNativeLoader);

    Target PackageTracerHome => _ => _
        .Description("Packages the already built src")
        .After(Clean, BuildTracerHome)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(ZipTracerHome)
        .DependsOn(BuildMsi)
        .DependsOn(PackNuGet);

    Target PackageMonitoringHomeBeta => _ => _
        .Description("Packages the already built src")
        .After(Clean, BuildTracerHome, BuildProfilerHome, BuildNativeLoader)
        .DependsOn(BuildMsi);

    Target BuildAndRunManagedUnitTests => _ => _
        .Description("Builds the managed unit tests and runs them")
        .After(Clean, BuildTracerHome)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(BuildRunnerTool)
        .DependsOn(CompileManagedUnitTests)
        .DependsOn(RunManagedUnitTests);

    Target BuildAndRunNativeUnitTests => _ => _
        .Description("Builds the native unit tests and runs them")
        .After(Clean, BuildTracerHome)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(CompileNativeTests)
        .DependsOn(RunNativeTests);

    Target BuildWindowsIntegrationTests => _ => _
        .Unlisted()
        .Requires(() => IsWin)
        .Description("Builds the integration tests for Windows")
        .DependsOn(CompileDependencyLibs)
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CreatePlatformlessSymlinks)
        .DependsOn(CompileSamples)
        .DependsOn(PublishIisSamples)
        .DependsOn(CompileIntegrationTests)
        .DependsOn(BuildNativeLoader)
        .DependsOn(BuildRunnerTool);

    Target BuildWindowsRegressionTests => _ => _
        .Unlisted()
        .Requires(() => IsWin)
        .Description("Builds the regression tests for Windows")
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CreatePlatformlessSymlinks)
        .DependsOn(CompileRegressionDependencyLibs)
        .DependsOn(CompileRegressionSamples)
        .DependsOn(CompileFrameworkReproductions)
        .DependsOn(CompileIntegrationTests)
        .DependsOn(BuildNativeLoader);

    Target BuildAndRunWindowsIntegrationTests => _ => _
        .Requires(() => IsWin)
        .Description("Builds and runs the Windows (non-IIS) integration tests")
        .DependsOn(BuildWindowsIntegrationTests)
        .DependsOn(RunWindowsIntegrationTests);

    Target BuildAndRunWindowsRegressionTests => _ => _
        .Requires(() => IsWin)
        .Description("Builds and runs the Windows regression tests")
        .DependsOn(BuildWindowsRegressionTests)
        .DependsOn(RunWindowsRegressionTests);

    Target BuildAndRunWindowsIisIntegrationTests => _ => _
        .Requires(() => IsWin)
        .Description("Builds and runs the Windows IIS integration tests")
        .DependsOn(BuildWindowsIntegrationTests)
        .DependsOn(RunWindowsIisIntegrationTests);

    Target BuildLinuxIntegrationTests => _ => _
        .Requires(() => !IsWin)
        .Description("Builds the linux integration tests")
        .DependsOn(CompileDependencyLibs)
        .DependsOn(CompileRegressionDependencyLibs)
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CompileSamplesLinux)
        .DependsOn(CompileMultiApiPackageVersionSamples)
        .DependsOn(CompileLinuxIntegrationTests)
        .DependsOn(BuildNativeLoader)
        .DependsOn(BuildRunnerTool);

    Target BuildAndRunLinuxIntegrationTests => _ => _
        .Requires(() => !IsWin)
        .Description("Builds and runs the linux integration tests. Requires docker-compose dependencies")
        .DependsOn(BuildLinuxIntegrationTests)
        .DependsOn(RunLinuxIntegrationTests);

    Target BuildAndRunToolArtifactTests => _ => _
       .Description("Builds and runs the tool artifacts tests")
       .DependsOn(CompileManagedTestHelpers)
       .DependsOn(InstallDdTraceTool)
       .DependsOn(BuildToolArtifactTests)
       .DependsOn(RunToolArtifactTests);

    Target PackNuGet => _ => _
        .Description("Creates the NuGet packages from the compiled src directory")
        .After(Clean, CompileManagedSrc)
        .DependsOn(CreateRequiredDirectories)
        .Executes(() =>
        {
            DotNetPack(s => s
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetConfiguration(BuildConfiguration)
                    .SetProperty("Platform", "AnyCPU")
                    .SetOutputDirectory(ArtifactsDirectory / "nuget")
                    .CombineWith(ProjectsToPack, (x, project) => x
                        .SetProject(project)),
                degreeOfParallelism: 2);
        });

    Target BuildDistributionNuget => _ => _
        // Currently requires manual copying of files into expected locations
        .Unlisted()
        .After(CreateDistributionHome)
        .Executes(() =>
        {
            DotNetBuild(x => x
                .SetProjectFile(Solution.GetProject(Projects.DatadogMonitoringDistribution))
                .EnableNoRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool"));
        });

    Target BuildRunnerTool => _ => _
        // Currently requires manual copying of files into expected locations
        .Unlisted()
        .After(CreateDistributionHome)
        .Executes(() =>
        {
            DotNetBuild(x => x
                .SetProjectFile(Solution.GetProject(Projects.Tool))
                .EnableNoRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool")
                .SetProperty("BuildStandalone", "false"));
        });

    Target BuildStandaloneTool => _ => _
        // Currently requires manual copying of files into expected locations
        .Unlisted()
        .After(CreateDistributionHome)
        .Executes(() =>
        {
            var runtimes = new[] { "win-x86", "win-x64", "linux-x64", "linux-musl-x64", "osx-x64", "linux-arm64" };
            DotNetPublish(x => x
                .SetProject(Solution.GetProject(Projects.Tool))
                // Have to do a restore currently as we're specifying specific runtime
                // .EnableNoRestore()
                .EnableNoDependencies()
                .SetFramework(TargetFramework.NETCOREAPP3_1)
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool")
                .SetProperty("BuildStandalone", "true")
                .CombineWith(runtimes, (c, runtime) => c
                                .SetRuntime(runtime)));
        });

    Target RunBenchmarks => _ => _
        .After(BuildTracerHome)
        .Description("Runs the Benchmarks project")
        .Executes(() =>
        {
            var benchmarksProject = Solution.GetProject(Projects.BenchmarksTrace);
            var resultsDirectory = benchmarksProject.Directory / "BenchmarkDotNet.Artifacts" / "results";
            EnsureCleanDirectory(resultsDirectory);

            try
            {
                DotNetBuild(s => s
                    .SetProjectFile(benchmarksProject)
                    .SetConfiguration(BuildConfiguration)
                    .SetFramework(TargetFramework.NETCOREAPP3_1)
                    .EnableNoDependencies()
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
                );

                DotNetRun(s => s
                    .SetProjectFile(benchmarksProject)
                    .SetConfiguration(BuildConfiguration)
                    .SetFramework(TargetFramework.NETCOREAPP3_1)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetApplicationArguments($"-r net472 netcoreapp3.1 -m -f {Filter ?? "*"} --iterationTime 2000")
                    .SetProcessEnvironmentVariable("DD_SERVICE", "dd-trace-dotnet")
                    .SetProcessEnvironmentVariable("DD_ENV", "CI")
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
                );
            }
            finally
            {
                if (Directory.Exists(resultsDirectory))
                {
                    CopyDirectoryRecursively(resultsDirectory, BuildDataDirectory / "benchmarks",
                                             DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
                }
            }
        });

    /// <summary>
    /// Run the default build
    /// </summary>
    public static int Main() => Execute<Build>(x => x.BuildTracerHome);
}
