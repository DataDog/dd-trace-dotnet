using System;
using System.IO;
using System.Linq;
using Nuke.Common;
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
    readonly MSBuildTargetPlatform Platform = MSBuildTargetPlatform.x64;
    
    [Parameter("The TargetFramework to execute when running or building a sample app, or linux integration tests")] 
    readonly TargetFramework Framework;

    [Parameter("Should all versions of integration NuGet packages be tested, or just the defaults")]
    readonly bool TestAllPackageVersions;

    [Parameter("The location to create the tracer home directory. Default is ./bin/tracer-home ")]
    readonly AbsolutePath TracerHome;
    [Parameter("The location to create the dd-trace home directory. Default is ./bin/dd-tracer-home ")]
    readonly AbsolutePath DDTracerHome;
    [Parameter("The location to place NuGet packages and other packages. Default is ./bin/artifacts ")]
    readonly AbsolutePath Artifacts;
    
    [Parameter("The location to restore Nuget packages (optional) ")]
    readonly AbsolutePath NugetPackageDirectory;

    [Parameter("Is the build running on Alpine linux? Default is 'false'")]
    readonly bool IsAlpine = false;

    [Parameter("The build version. Default is latest")]
    readonly string Version = "1.28.1";

    [Parameter("Whether the build version is a prerelease(for packaging purposes). Default is latest")]
    readonly bool IsPrerelease = true;

    [Parameter("Prints the available drive space before executing each target. Defaults to false")]
    readonly bool PrintDriveSpace = false;

    [Parameter("Override the default test filters for integration tests. (Optional)")]
    readonly string Filter;

    [Parameter("Enables code coverage")]
    readonly bool CodeCoverage;

    Target Info => _ => _
        .Description("Describes the current configuration")
        .Before(Clean, Restore, BuildTracerHome)
        .Executes(() =>
        {
            Logger.Info($"Configuration: {BuildConfiguration}");
            Logger.Info($"Platform: {Platform}");
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
        .Executes(()=>
        {
            if(IsWin)
            {
                // These are created as part of the CreatePlatformlessSymlinks target and cause havok
                // when deleting directories otherwise
                DeleteReparsePoints(SourceDirectory);
                DeleteReparsePoints(TestsDirectory);
            }
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => DeleteDirectory(x));
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => DeleteDirectory(x));
            EnsureCleanDirectory(OutputDirectory);
            EnsureCleanDirectory(TracerHomeDirectory);
            EnsureCleanDirectory(DDTracerHomeDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(NativeProfilerProject.Directory / "build");
            EnsureCleanDirectory(NativeProfilerProject.Directory / "deps");
            EnsureCleanDirectory(BuildDataDirectory);
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
        .DependsOn(CopyIntegrationsJson)
        .DependsOn(CreateDdTracerHome);


    Target PackageTracerHome => _ => _
        .Description("Packages the already built src")
        .After(Clean, BuildTracerHome)
        .DependsOn(CreateRequiredDirectories)
        .DependsOn(ZipTracerHome)
        .DependsOn(BuildMsi)
        .DependsOn(PackNuGet);

    Target BuildAndRunManagedUnitTests => _ => _
        .Description("Builds the managed unit tests and runs them")
        .After(Clean, BuildTracerHome)
        .DependsOn(CreateRequiredDirectories)
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
        .DependsOn(CompileIntegrationTests);

    Target BuildWindowsRegressionIntegrationTests => _ => _
        .Unlisted()
        .Requires(() => IsWin)
        .Description("Builds the integration tests for Windows")
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CreatePlatformlessSymlinks)
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
        .DependsOn(BuildWindowsRegressionIntegrationTests)
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
        .DependsOn(CompileLinuxIntegrationTests);

    Target BuildAndRunLinuxIntegrationTests => _ => _
        .Requires(() => IsLinux)
        .Description("Builds and runs the linux integration tests. Requires docker-compose dependencies")
        .DependsOn(BuildLinuxIntegrationTests)
        .DependsOn(RunLinuxIntegrationTests);

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

    Target BuildRunnerTool => _ => _
        // Currently requires manual copying of files into expected locations
        .Unlisted()
        .Executes(() =>
        {
            DotNetBuild(x => x
                .SetProjectFile(Solution.GetProject(Projects.RunnerTool))
                .EnableNoRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool"));
        });

    Target BuildStandaloneTool => _ => _
        // Currently requires manual copying of files into expected locations
        .Unlisted()
        .Executes(() =>
        {
            var runtimes = new[] {"win-x86", "win-x64", "linux-x64", "linux-musl-x64", "osx-x64", "linux-arm64"};
            DotNetPublish(x => x
                .SetProject(Solution.GetProject(Projects.StandaloneTool))
                // Have to do a restore currently as we're specifying specific runtime
                // .EnableNoRestore()
                .EnableNoDependencies()
                .SetFramework(TargetFramework.NETCOREAPP3_1)
                .SetConfiguration(BuildConfiguration)
                .SetNoWarnDotNetCore3()
                .SetDDEnvironmentVariables("dd-trace-dotnet-runner-tool")
                .CombineWith(runtimes, (c, runtime) => c
                    .SetRuntime(runtime)));
                
        });

    Target RunBenchmarks => _ => _
        .After(BuildTracerHome)
        .Description("Runs the Benchmarks project")
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution.GetProject(Projects.BenchmarksTrace))
                .SetConfiguration(BuildConfiguration)
                .SetFramework(TargetFramework.NETCOREAPP3_1)
                .EnableNoDependencies()
                .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
            );

            DotNetRun(s => s
                .SetProjectFile(Solution.GetProject(Projects.BenchmarksTrace))
                .SetConfiguration(BuildConfiguration)
                .SetFramework(TargetFramework.NETCOREAPP3_1)
                .EnableNoRestore()
                .EnableNoBuild()
                .SetApplicationArguments("-r net472 netcoreapp3.1 -m -f * --iterationTime 2000")
                .SetProcessEnvironmentVariable("DD_SERVICE", "dd-trace-dotnet")
                .SetProcessEnvironmentVariable("DD_ENV", "CI")
                .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
            );
        });

    /// <summary>
    /// Run the default build
    /// </summary>
    public static int Main() => Execute<Build>(x => x.BuildTracerHome);
}
