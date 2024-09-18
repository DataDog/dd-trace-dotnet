using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Colorful;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Logger = Serilog.Log;

// #pragma warning disable SA1306
// #pragma warning disable SA1134
// #pragma warning disable SA1111
// #pragma warning disable SA1400
// #pragma warning disable SA1401

[ShutdownDotNetAfterServerBuild, BuildFinishedNotification]
partial class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration BuildConfiguration = Configuration.Release;

    [Parameter("Platform to build - x86, x64, ARM64. Defaults to the current platform.")]
    readonly MSBuildTargetPlatform TargetPlatform = GetDefaultTargetPlatform();

    [Parameter("The TargetFramework to execute when running or building a sample app, or linux integration tests")]
    readonly TargetFramework Framework;

    [Parameter("Should all versions of integration NuGet packages be tested")]
    readonly bool TestAllPackageVersions;

    [Parameter("Should minor versions of integration NuGet packages be included")]
    readonly bool IncludeMinorPackageVersions;

    [Parameter("The location to create the monitoring home directory. Default is ./shared/bin/monitoring-home ")]
    readonly AbsolutePath MonitoringHome;
    [Parameter("The location to place NuGet packages and other packages. Default is ./bin/artifacts ")]
    readonly AbsolutePath Artifacts;

    [Parameter("The location to restore Nuget packages (optional) ")]
    readonly AbsolutePath NugetPackageDirectory;

    [Parameter("Is the build running on Alpine linux? Default is 'false'")]
    readonly bool IsAlpine = false;

    [Parameter("The current latest tracer version")]
    const int LatestMajorVersion = 3;

    [Parameter("The current version of the source and build")]
    readonly string Version = "3.3.1";

    [Parameter("Whether the current build version is a prerelease(for packaging purposes)")]
    readonly bool IsPrerelease = false;

    [Parameter("The new build version to set")]
    readonly string NewVersion;

    [Parameter("Whether the new build version is a prerelease(for packaging purposes)")]
    readonly bool? NewIsPrerelease;

    [Parameter("Prints the available drive space before executing each target. Defaults to false")]
    readonly bool PrintDriveSpace = false;

    [Parameter("Override the default test filters for integration tests. (Optional)")]
    readonly string Filter;

    [Parameter("Override the default category filter for running benchmarks. (Optional)")]
    readonly string BenchmarkCategory;

    [Parameter("Enables code coverage")]
    readonly bool CodeCoverageEnabled;

    [Parameter("Enable or Disable fast developer loop")]
    readonly bool FastDevLoop;
    
    [Parameter("The directory containing the tool .nupkg file")]
    readonly AbsolutePath ToolSource;

    [Parameter("The directory to install the tool to")]
    readonly AbsolutePath ToolDestination;

    [Parameter("Should we build and run tests that require docker. true = only docker integration tests, false = no docker integration tests, null = all", List = false)]
    readonly bool? IncludeTestsRequiringDocker;

    [Parameter("Should we build and run tests against _all_ target frameworks, or just the reduced set. Defaults to true locally, false in PRs, and true in CI on main branch only", List = false)]
    readonly bool IncludeAllTestFrameworks = true;

    [Parameter("Should we build native binaries as Universal. Default to false, so we can still build native libs outside of docker.")]
    readonly bool AsUniversal = false;
    
    [Parameter("RuntimeIdentifier sets the target platform for ReadyToRun assemblies in 'PublishManagedTracerR2R'." +
               "See https://learn.microsoft.com/en-us/dotnet/core/rid-catalog")]
    string RuntimeIdentifier { get; }
    
    public Build()
    {
        RuntimeIdentifier = GetDefaultRuntimeIdentifier(IsAlpine);
    }

    Target Info => _ => _
                       .Description("Describes the current configuration")
                       .Before(Clean, Restore, BuildTracerHome)
                       .Executes(() =>
                        {
                            Logger.Information($"Configuration: {BuildConfiguration}");
                            Logger.Information($"TargetPlatform: {TargetPlatform}");
                            Logger.Information($"Framework: {Framework}");
                            Logger.Information($"TestAllPackageVersions: {TestAllPackageVersions}");
                            Logger.Information($"MonitoringHomeDirectory: {MonitoringHomeDirectory}");
                            Logger.Information($"ArtifactsDirectory: {ArtifactsDirectory}");
                            Logger.Information($"NugetPackageDirectory: {NugetPackageDirectory}");
                            Logger.Information($"IncludeAllTestFrameworks: {IncludeAllTestFrameworks}");
                            Logger.Information($"IsAlpine: {IsAlpine}");
                            Logger.Information($"Version: {Version}");
                            Logger.Information($"RuntimeIdentifier: {RuntimeIdentifier}");
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
            BundleHomeDirectory.GlobFiles("**").ForEach(x => DeleteFile(x));
            BenchmarkHomeDirectory.GlobFiles("**").ForEach(x => DeleteFile(x));
            EnsureCleanDirectory(BuildArtifactsDirectory);
            EnsureCleanDirectory(MonitoringHomeDirectory);
            EnsureCleanDirectory(OutputDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(NativeTracerProject.Directory / "build");
            EnsureCleanDirectory(NativeTracerProject.Directory / "deps");
            EnsureCleanDirectory(BuildDataDirectory);
            EnsureCleanDirectory(ExplorationTestsDirectory);
            DeleteFile(WindowsTracerHomeZip);

            EnsureCleanDirectory(ProfilerOutputDirectory);

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

    Target BuildNativeTracerHome => _ => _
        .Unlisted()
        .Description("Builds the native src ")
        .After(Clean, CompileManagedLoader)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(CompileTracerNativeSrc)
        .DependsOn(PublishNativeTracer);


    Target BuildManagedTracerHome => _ => _
        .Unlisted()
        .Description("Builds the native and managed src, and publishes the tracer home directory")
        .After(Clean, BuildNativeTracerHome)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(Restore)
        .DependsOn(CompileManagedSrc)
        .DependsOn(PublishManagedTracer)
        .DependsOn(DownloadLibDdwaf)
        .DependsOn(CopyLibDdwaf)
        .DependsOn(CreateMissingNullabilityFile)
        .DependsOn(CreateRootDescriptorsFile);
    
    Target BuildManagedTracerHomeR2R => _ => _
        .Unlisted()
        .Description("Builds the native and managed src, and publishes the tracer home directory")
        .After(Clean, BuildNativeTracerHome)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(Restore)
        .DependsOn(CompileManagedSrc)
        .DependsOn(PublishManagedTracerR2R)
        .DependsOn(DownloadLibDdwaf)
        .DependsOn(CopyLibDdwaf)
        .DependsOn(CreateMissingNullabilityFile)
        .DependsOn(CreateRootDescriptorsFile);

    Target BuildTracerHome => _ => _
        .Description("Builds the native and managed src, and publishes the tracer home directory")
        .After(Clean)
        .DependsOn(CompileManagedLoader, BuildNativeTracerHome, BuildManagedTracerHome, BuildNativeLoader);

    Target BuildProfilerHome => _ => _
        .Description("Builds the Profiler native and managed src, and publishes the profiler home directory")
        .After(Clean)
        .DependsOn(CompileProfilerNativeSrc)
        .DependsOn(PublishProfiler);

    Target BuildNativeLoader => _ => _
        .Description("Builds the Native Loader, and publishes to the monitoring home directory")
        .After(Clean)
        .DependsOn(CompileNativeLoader)
        .DependsOn(PublishNativeLoader);

    Target BuildNativeWrapper => _ => _
        .Description("")
        .After(Clean)
        .DependsOn(CompileNativeWrapper)
        .DependsOn(TestNativeWrapper)
        .DependsOn(PublishNativeWrapper);

    Target PackageTracerHome => _ => _
        .Description("Builds NuGet packages, MSIs, and zip files, from already built source")
        .After(Clean, BuildTracerHome, BuildProfilerHome, BuildNativeLoader)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(ZipMonitoringHome)
        .DependsOn(BuildMsi)
        .DependsOn(PackNuGet);

    Target BuildAndRunManagedUnitTests => _ => _
        .Description("Builds the managed unit tests and runs them")
        .After(Clean, BuildTracerHome, BuildProfilerHome)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(BuildRunnerTool)
        .DependsOn(CompileManagedUnitTests)
        .DependsOn(RunManagedUnitTests);

    Target RunNativeUnitTests => _ => _
        .Description("Builds the native unit tests and runs them")
        .After(Clean, BuildTracerHome, BuildProfilerHome)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(RunNativeTests);

    Target BuildWindowsIntegrationTests => _ => _
        .Unlisted()
        .Requires(() => IsWin)
        .Description("Builds the integration tests for Windows")
        .DependsOn(CompileDependencyLibs)
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CompileSamplesWindows)
        .DependsOn(CompileIntegrationTests)
        .DependsOn(BuildRunnerTool);

    Target BuildAspNetIntegrationTests => _ => _
        .Unlisted()
        .Requires(() => IsWin)
        .Description("Builds the ASP.NET integration tests for Windows")
        .DependsOn(CompileDependencyLibs)
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(PublishIisSamples)
        .DependsOn(CompileIntegrationTests);

    Target BuildWindowsRegressionTests => _ => _
        .Unlisted()
        .Requires(() => IsWin)
        .Description("Builds the regression tests for Windows")
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CompileRegressionDependencyLibs)
        .DependsOn(CompileRegressionSamples)
        .DependsOn(CompileFrameworkReproductions)
        .DependsOn(CompileIntegrationTests);

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

    Target BuildAndRunWindowsAzureFunctionsTests => _ => _
        .Requires(() => IsWin)
        .Description("Builds and runs the Windows Azure Functions tests")
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CompileAzureFunctionsSamplesWindows)
        .DependsOn(BuildRunnerTool)
        .DependsOn(CompileIntegrationTests)
        .DependsOn(RunWindowsAzureFunctionsTests);

    Target BuildLinuxIntegrationTests => _ => _
        .Requires(() => !IsWin)
        .Description("Builds the linux integration tests")
        .DependsOn(CompileDependencyLibs)
        .DependsOn(CompileRegressionDependencyLibs)
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CompileSamplesLinuxOrOsx)
        .DependsOn(CompileMultiApiPackageVersionSamples)
        .DependsOn(CompileLinuxOrOsxIntegrationTests)
        .DependsOn(CompileLinuxDdDotnetIntegrationTests)
        .DependsOn(BuildRunnerTool)
        .DependsOn(CopyServerlessArtifacts);

    Target BuildAndRunLinuxIntegrationTests => _ => _
        .Requires(() => !IsWin)
        .Description("Builds and runs the linux integration tests. Requires docker-compose dependencies")
        .DependsOn(BuildLinuxIntegrationTests)
        .DependsOn(RunLinuxIntegrationTests)
        .DependsOn(RunLinuxDdDotnetIntegrationTests);

    Target BuildOsxIntegrationTests => _ => _
        .Requires(() => IsOsx)
        .Description("Builds the osx integration tests")
        .DependsOn(CompileDependencyLibs)
        .DependsOn(CompileRegressionDependencyLibs)
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CompileSamplesLinuxOrOsx)
        .DependsOn(CompileMultiApiPackageVersionSamples)
        .DependsOn(CompileLinuxOrOsxIntegrationTests)
        .DependsOn(BuildRunnerTool)
        .DependsOn(CopyServerlessArtifacts);

    Target BuildAndRunOsxIntegrationTests => _ => _
        .Requires(() => IsOsx)
        .Description("Builds and runs the osx integration tests. Requires docker-compose dependencies")
        .DependsOn(BuildOsxIntegrationTests)
        .DependsOn(RunOsxIntegrationTests);

    Target BuildAndRunToolArtifactTests => _ => _
       .Description("Builds and runs the tool artifacts tests")
       .DependsOn(CompileManagedTestHelpers)
       .DependsOn(InstallDdTraceTool)
       .DependsOn(BuildToolArtifactTests)
       .DependsOn(RunToolArtifactTests);

    Target BuildAndRunDdDotnetArtifactTests => _ => _
        .Description("Builds and runs the tool artifacts tests")
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(BuildDdDotnetArtifactTests)
        .DependsOn(CopyDdDotnet)
        .DependsOn(RunDdDotnetArtifactTests);

    Target PackNuGet => _ => _
        .Description("Creates the NuGet packages from the compiled src directory")
        .After(Clean, CompileManagedSrc)
        .DependsOn(CreateRequiredDirectories, CreateRootDescriptorsFile)
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

    Target BuildBundleNuget => _ => _
        .Unlisted()
        .After(CreateBundleHome, ExtractDebugInfoLinux)
        .Executes(() =>
        {
            DotNetBuild(x => x
                .SetProjectFile(Solution.GetProject(Projects.DatadogTraceBundle))
                .EnableNoRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetProperty("PackageOutputPath", ArtifactsDirectory / "nuget" / "bundle")
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool"));
        });

    Target BuildBenchmarkNuget => _ => _
        .Unlisted()
        .DependsOn(CreateBenchmarkIntegrationHome)
        .After(ExtractDebugInfoLinux)
        .Executes(() =>
        {
            DotNetBuild(x => x
                .SetProjectFile(Solution.GetProject(Projects.DatadogTraceBenchmarkDotNet))
                .EnableNoRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetProperty("PackageOutputPath", ArtifactsDirectory / "nuget" / "benchmark")
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool"));
        });

    Target BuildDdDotnet => _ => _
        .Unlisted()
        .Executes(() =>
        {
            var framework = Framework ?? TargetFramework.NET8_0;
            DotNetBuild(x => x
                .SetProjectFile(Solution.GetProject(Projects.DdDotnet))
                .SetConfiguration(BuildConfiguration)
                .SetFramework(framework)
                .SetNoWarnDotNetCore3());

            string rid;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                rid = "win-x64";
            }
            else
            {
                rid = (IsLinux, IsArm64) switch
                {
                    (true, false) => IsAlpine ? "linux-musl-x64" : "linux-x64",
                    (true, true) => IsAlpine ? "linux-musl-arm64" : "linux-arm64",
                    (false, false) => "osx-x64",
                    (false, true) => "osx-arm64",
                };
            }

            // We don't want the symbols in MonitoringHome,
            // So we publish to a different folder than copy only the executable
            var publishFolder = ArtifactsDirectory / "dd-dotnet" / rid;

            DotNetPublish(x => x
                .SetProject(Solution.GetProject(Projects.DdDotnet))
                .SetFramework(framework)
                .SetRuntime(rid)
                .SetConfiguration(BuildConfiguration)
                .SetOutput(publishFolder));

            var file = IsWin ? "dd-dotnet.exe" : "dd-dotnet";
            CopyFileToDirectory(publishFolder / file, MonitoringHomeDirectory / rid, FileExistsPolicy.Overwrite);
        });

    Target BuildRunnerTool => _ => _
        .Unlisted()
        .DependsOn(CompileInstrumentationVerificationLibrary)
        .After(CreateBundleHome, ExtractDebugInfoLinux)
        .Executes(() =>
        {
            DotNetBuild(x => x
                .SetProjectFile(Solution.GetProject(Projects.DdTrace))
                .EnableNoRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool")
                .SetProperty("PackageOutputPath", ArtifactsDirectory / "nuget" / "dd-trace")
                .SetProperty("BuildStandalone", "false")
                .SetProcessEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1"));
        });

    Target PackRunnerToolNuget => _ => _
        .Unlisted()
        .After(CreateBundleHome, ExtractDebugInfoLinux, BuildRunnerTool)
        .Executes(() =>
        {
            DotNetPack(x => x
                .SetProject(Solution.GetProject(Projects.DdTrace))
                .SetConfiguration(BuildConfiguration)
                .EnableNoDependencies()
                .SetNoWarnDotNetCore3()
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool")
                .SetProperty("PackageOutputPath", ArtifactsDirectory / "nuget" / "dd-trace")
                .SetProperty("BuildStandalone", "false")
                .SetProperty("DebugSymbols", "False")
                .SetProperty("DebugType", "None")
                .SetProperty("GenerateDocumentationFile", "False")
                .SetProcessEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1"));
        });

    Target BuildStandaloneTool => _ => _
        .Unlisted()
        .After(CreateBundleHome, ExtractDebugInfoLinux, PackRunnerToolNuget)
        .Executes(() =>
        {
            var runtimes = new[]
            {
                (rid: "win-x86", archiveFormat: ".zip"),
                (rid: "win-x64", archiveFormat: ".zip"),
                (rid: "linux-x64", archiveFormat: ".tar.gz"),
                (rid: "linux-musl-x64", archiveFormat: ".tar.gz"),
                (rid: "osx-x64", archiveFormat: ".tar.gz"),
                (rid: "linux-arm64", archiveFormat: ".tar.gz"),
                (rid: "linux-musl-arm64", archiveFormat: ".tar.gz"),
            }.Select(x => (x.rid, archive: ArtifactsDirectory / $"dd-trace-{x.rid}{x.archiveFormat}", output: ArtifactsDirectory / "tool" / x.rid))
             .ToArray();

            runtimes.ForEach(runtime => EnsureCleanDirectory(runtime.output));
            runtimes.ForEach(runtime => DeleteFile(runtime.archive));

            DotNetPublish(x => x
                .SetProject(Solution.GetProject(Projects.DdTrace))
                // Have to do a restore currently as we're specifying specific runtime
                // .EnableNoRestore()
                .EnableNoDependencies()
                .SetFramework(TargetFramework.NET7_0)
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool")
                .SetProperty("BuildStandalone", "true")
                .SetProperty("DebugSymbols", "False")
                .SetProperty("DebugType", "None")
                .SetProperty("GenerateDocumentationFile", "False")
                .SetProcessEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1")
                .CombineWith(runtimes, (c, runtime) => c
                                .SetProperty("PublishDir", runtime.output)
                                .SetRuntime(runtime.rid)));

            runtimes.ForEach(
                x => Compress(x.output, x.archive));
        });

    Target RunBenchmarks => _ => _
        .After(BuildTracerHome)
        .After(BuildProfilerHome)
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
                    .EnableNoDependencies()
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
                );

                var (framework, runtimes) = IsOsx switch
                {
                    true => (TargetFramework.NETCOREAPP3_1, "net6.0"),
                    false => (TargetFramework.NET6_0, "net472 netcoreapp3.1 net6.0"),
                };

                DotNetRun(s => s
                    .SetProjectFile(benchmarksProject)
                    .SetConfiguration(BuildConfiguration)
                    .SetFramework(framework)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetApplicationArguments($"-r {runtimes} -m -f {Filter ?? "*"} --anyCategories {BenchmarkCategory ?? "tracer"} --iterationTime 2000")
                    .SetProcessEnvironmentVariable("DD_SERVICE", "dd-trace-dotnet")
                    .SetProcessEnvironmentVariable("DD_ENV", "CI")
                    .SetProcessEnvironmentVariable("DD_DOTNET_TRACER_HOME", MonitoringHome)
                    .SetProcessEnvironmentVariable("DD_TRACER_HOME", MonitoringHome)

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

                CopyDumpsToBuildData();
            }
        });

    /// <summary>
    /// Run the default build
    /// </summary>
    public static int Main() => Execute<Build>(x => x.BuildTracerHome);
}
