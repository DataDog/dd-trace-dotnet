using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static CustomDotNetTasks;

// #pragma warning disable SA1306  
// #pragma warning disable SA1134  
// #pragma warning disable SA1111  
// #pragma warning disable SA1400  
// #pragma warning disable SA1401  

[CheckBuildProjectConfigurations]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration Configuration = Configuration.Release;

    [Parameter("Platform to build - x86 or x64. Default is x64")]
    readonly MSBuildTargetPlatform Platform = MSBuildTargetPlatform.x64;

    [Parameter("The location to publish the build output. Default is ./src/bin/managed-publish ")]
    readonly AbsolutePath PublishOutput;
    
    [Parameter("The location to create the tracer home directory. Default is ./src/bin/tracer-home ")]
    readonly AbsolutePath TracerHome;
    [Parameter("The location to place NuGet packages and other packages. Default is ./src/bin/artifiacts ")]
    readonly AbsolutePath Artifacts;
    
    [Parameter("The location to restore Nuget packages (optional) ")]
    readonly AbsolutePath NugetManagedCacheFolder;
    
    [Solution("Datadog.Trace.sln")] readonly Solution Solution;
    [Solution("Datadog.Trace.Native.sln")] readonly Solution NativeSolution;
    AbsolutePath MsBuildProject => RootDirectory / "Datadog.Trace.proj";

    AbsolutePath PublishOutputPath => PublishOutput ?? (SourceDirectory / "bin" / "managed-publish");
    AbsolutePath TracerHomeDirectory => TracerHome ?? (RootDirectory / "bin" / "tracer-home");
    AbsolutePath ArtifactsDirectory => Artifacts ?? (RootDirectory / "bin" / "artifacts");
    AbsolutePath TracerHomeZip => ArtifactsDirectory / "tracer-home.zip";

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "test";
    
    Project ManagedLoaderProject => Solution.GetProject(Projects.ClrProfilerManagedLoader);
    Project ManagedProfilerProject => Solution.GetProject(Projects.ClrProfilerManaged);
    Project NativeProfilerProject => Solution.GetProject(Projects.ClrProfilerNative);
    Project WindowsInstallerProject => Solution.GetProject(Projects.WindowsInstaller);

    [LazyPathExecutable(name: "cmake")] readonly Lazy<Tool> CMake;
    [LazyPathExecutable(name: "make")] readonly Lazy<Tool> Make;
    [LazyLocalExecutable(@"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\gacutil.exe")] 
    readonly Lazy<Tool> GacUtil;
    
    IEnumerable<MSBuildTargetPlatform> ArchitecturesForPlatform =>
        Equals(Platform, MSBuildTargetPlatform.x64)
            ? new[] {MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86}
            : new[] {MSBuildTargetPlatform.x86};
    
    IEnumerable<Project> NuGetPackages => new []
    {
        Solution.GetProject("Datadog.Trace"),
        Solution.GetProject("Datadog.Trace.OpenTracing"),
    };
    
    IEnumerable<TargetFramework> TargetFrameworks = new []
    {
        TargetFramework.NET45, 
        TargetFramework.NET461,
        TargetFramework.NETSTANDARD2_0, 
        TargetFramework.NETCOREAPP3_1,
    };
    
    IEnumerable<string> GacProjects = new []
    {
        Projects.DatadogTrace,
        Projects.DatadogTraceAspNet,
        Projects.ClrProfilerManaged,
        Projects.ClrProfilerManagedCore,
    };

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => EnsureCleanDirectory(x));
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => EnsureCleanDirectory(x));
            EnsureCleanDirectory(PublishOutputPath);
            EnsureCleanDirectory(TracerHomeDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            DeleteFile(TracerHomeZip);
        });

    Target RestoreNuGet => _ => _
        .Unlisted()
        .After(Clean)
        .Executes(() =>
        {
            NuGetTasks.NuGetRestore(s => s
                .SetTargetPath(Solution)
                .SetVerbosity(NuGetVerbosity.Normal));
        });
 
    Target RestoreDotNet => _ => _
        .Unlisted()
        .After(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
                .SetVerbosity(DotNetVerbosity.Normal)
                .SetTargetPlatform(Platform) // necessary to ensure we restore every project
                .SetProperty("configuration", Configuration.ToString())
                // .SetNoWarnDotNetCore3()
                .When(!string.IsNullOrEmpty(NugetManagedCacheFolder), o => 
                        o.SetPackageDirectory(NugetManagedCacheFolder)));
        });

    Target Restore => _ => _
        .After(Clean)
        // .DependsOn(RestoreDotNet)
        .DependsOn(RestoreNuGet);

    Target CompileManagedSrc => _ => _
        .Description("Compiles the managed code in the src directory")
        .DependsOn(Restore)
        .Executes(() =>
        {
            // Always AnyCPU
            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(Configuration)
                .DisableRestore()
                .SetTargets("BuildCsharpSrc")
            );
        });

    Target PackNuGet => _ => _
        .Description("Creates the NuGet packages from the compiled src directory")
        .DependsOn(CompileManagedSrc)
        .Executes(() =>
        {
            DotNetPack(s => s
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(ArtifactsDirectory)
                    .CombineWith(NuGetPackages, (x, project) => x
                        .SetProject(project)),
                degreeOfParallelism: 2);
        });
    
    Target CompileNativeSrcWindows => _ => _
        .Unlisted()   
        .DependsOn(CompileManagedSrc)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // If we're building for x64, build for x86 too 
            var platforms =
                Equals(Platform, MSBuildTargetPlatform.x64)
                    ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                    : new[] { MSBuildTargetPlatform.x86 };

            // Can't use dotnet msbuild, as needs to use the VS version of MSBuild
            MSBuild(s => s
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(Configuration)
                .SetTargets("BuildCppSrc")
                .DisableRestore()
                .SetMaxCpuCount(null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });
    
    Target CompileNativeSrcLinux => _ => _
        .Unlisted()   
        .DependsOn(CompileManagedSrc)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var envVars = new Dictionary<string, string>
            {
                {"CXX", "clang++"},
                {"CC", "clang"},
            };
            CMake.Value(arguments: ".", environmentVariables: envVars);
            Make.Value();
        });

    Target CompileNativeSrc => _ => _
        .Description("Compiles the native loader")
        .DependsOn(CompileNativeSrcWindows)
        .DependsOn(CompileNativeSrcLinux);

    Target PublishManagedProfiler => _ => _
        .DependsOn(CompileManagedSrc)
        .Executes(() =>
        {
            DotNetPublish(s => s
                .SetProject(ManagedProfilerProject)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore()
                .CombineWith(TargetFrameworks, (p, framework) => p
                    .SetFramework(framework)
                    .SetOutput(TracerHomeDirectory / framework)));
        });
    
    Target PublishNativeProfilerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .DependsOn(CompileNativeSrcWindows)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatform)
            {
                var source = NativeProfilerProject.Directory / "bin" / Configuration / architecture.ToString() /
                             $"{NativeProfilerProject.Name}.dll";
                var dest = TracerHomeDirectory / $"win-{architecture}";
                Logger.Info($"Copying '{source}' to '{dest}'");
                CopyFileToDirectory(source, dest, FileExistsPolicy.OverwriteIfNewer);
            }
        });
    
    Target PublishNativeProfilerLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .DependsOn(CompileNativeSrcLinux)
        .Executes(() =>
        {
            // TODO: Linux: x64, arm64; alpine: x64
            foreach (var architecture in new []{ MSBuildTargetPlatform.x64})
            {
                var source = NativeProfilerProject.Directory / "bin" / Configuration / architecture.ToString() /
                             $"{NativeProfilerProject.Name}.so";
                var dest = TracerHomeDirectory / $"linux-{architecture}";
                Logger.Info($"Copying '{source}' to '{dest}'");
                CopyFileToDirectory(source, dest, FileExistsPolicy.OverwriteIfNewer);
            }
        });
    
    Target CopyIntegrationsJson => _ => _
        .After(Clean)
        .Executes(() =>
        {
            var source = RootDirectory / "integrations.json";
            var dest = TracerHomeDirectory;

            Logger.Info($"Copying '{source}' to '{dest}'");
            CopyFileToDirectory(source, dest, FileExistsPolicy.OverwriteIfNewer);
        });
    
    Target CompileManagedUnitTests => _ => _
        .DependsOn(Restore)
        .DependsOn(CompileManagedSrc)
        .Executes(() =>
        {
            // Always AnyCPU
            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(Configuration)
                .DisableRestore()
                .SetProperty("BuildProjectReferences", false)
                .SetTargets("BuildCsharpUnitTests"));
        });
    
    Target RunManagedUnitTests => _ => _
        .DependsOn(CompileManagedUnitTests)
        .Executes(() =>
        {
            var testProjects = RootDirectory.GlobFiles("test/**/*.Tests.csproj");

            DotNetTest(x => x
                .EnableNoRestore()
                .EnableNoBuild()
                .SetConfiguration(Configuration)
                .SetDDEnvironmentVariables()
                .CombineWith(testProjects, (x, project) => x
                    .SetProjectFile(project)));
        });

    Target CompileDependencyLibs => _ => _
        .DependsOn(Restore)
        .DependsOn(CompileManagedSrc)
        .Executes(() =>
        {
            // Always AnyCPU
            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(Configuration)
                .DisableRestore()
                .SetNoDependencies()
                .SetTargets("BuildDependencyLibs")
            );
        });
    
    Target CompileSamples => _ => _
        .After(CompileDependencyLibs)
        .Executes(() =>
        {
            DotNetMSBuild(config => config
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(Configuration)
                .DisableRestore()
                .SetNoDependencies()
                .SetProperty("BuildInParallel", "false")
                .SetProperty("ManagedProfilerOutputDirectory", PublishOutputPath)
                .SetProperty("ExcludeManagedProfiler", true)
                .SetProperty("ExcludeNativeProfiler", true)
                .SetProperty("LoadManagedProfilerFromProfilerDirectory", false)
                .SetTargets("SampleLibs")
                .CombineWith(ArchitecturesForPlatform.Reverse(), (o, arch) => 
                    o.SetTargetPlatform(arch)));
        });

    Target BuildTracerHome => _ => _
        .Description("Builds the tracer home directory from already-compiled sources")
        .DependsOn(CompileManagedSrc)
        .DependsOn(PublishManagedProfiler)
        .DependsOn(PublishNativeProfilerWindows)
        .DependsOn(PublishNativeProfilerLinux)
        .DependsOn(CopyIntegrationsJson);
    
    Target ZipTracerHome => _ => _
        .Unlisted()
        .DependsOn(BuildTracerHome)
        .Executes(() =>
        {
            CompressZip(TracerHomeDirectory, TracerHomeZip, fileMode: FileMode.Create);
        });

    Target BuildMsi => _ => _
        .Description("Builds the .msi files from the compiled tracer home directory")
        .DependsOn(BuildTracerHome)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            MSBuild(s => s
                    .SetTargetPath(WindowsInstallerProject)
                    .SetConfiguration(Configuration)
                    .AddProperty("RunWixToolsOutOfProc", true)
                    .SetProperty("TracerHomeDirectory", TracerHomeDirectory)
                    .SetMaxCpuCount(null)
                    .CombineWith(ArchitecturesForPlatform, (o, arch) => o
                        .SetProperty("MsiOutputPath", ArtifactsDirectory / arch.ToString())
                        .SetTargetPlatform(arch)),
                degreeOfParallelism: 2);
        });

    Target CompileFrameworkReproductions => _ => _
        .After(CompileManagedSrc)
        .Requires(() => PublishOutputPath != null)
        .Executes(() =>
        {
            // this triggers a dependency chain that builds all the managed and native dlls
            DotNetMSBuild(s => s
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(Configuration)
                .SetTargetPlatform(Platform)
                .SetTargets("BuildFrameworkReproductions")
                .SetMaxCpuCount(null));
        });

    Target GacAdd => _ => _
        .Description("Adds the (already built) files to the Windows GAC **REQUIRES ELEVATED PERMISSIONS** ")
        .Requires(() => IsWin)
        .After(BuildTracerHome)
        .Executes(() =>
        {
            foreach (var dll in GacProjects)
            {
                var path = TracerHomeDirectory / "net461" / $"{dll}.dll";
                GacUtil.Value($"/i \"{path}\"");
            }
        });
    
    Target GacRemove => _ => _
        .Description("Removes the Datadog tracer files from the Windows GAC **REQUIRES ELEVATED PERMISSIONS** ")
        .Requires(() => IsWin)
        .Executes(() =>
        {
            foreach (var dll in GacProjects)
            {
                GacUtil.Value($"/u \"{dll}\"");
            }
        });
    
    Target RunNativeTests => _ => _
        .Executes(() =>
        {
            var workingDirectory = TestsDirectory / "Datadog.Trace.ClrProfiler.Native.Tests" / "bin" / Configuration.ToString() / Platform.ToString();
            var exePath = workingDirectory / "Datadog.Trace.ClrProfiler.Native.Tests.exe";
            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe("--gtest_output=xml", workingDirectory: workingDirectory);
        });

    Target LocalBuild => _ =>
        _
            .Description("Compiles the source and builds the tracer home directory for local usage")
            .DependsOn(RestoreNuGet)
            .DependsOn(CompileManagedSrc)
            .DependsOn(CompileNativeSrc)
            .DependsOn(BuildTracerHome);

    Target WindowsFullCiBuild => _ =>
        _
            .Description("Convenience method for running the same build steps as the full Windows CI build")
            .DependsOn(Clean)
            .DependsOn(RestoreNuGet)
            .DependsOn(CompileManagedSrc)
            .DependsOn(CompileNativeSrc)
            .DependsOn(BuildTracerHome)
            .DependsOn(ZipTracerHome)
            .DependsOn(PackNuGet)
            .DependsOn(BuildMsi)
            .DependsOn(CompileManagedUnitTests)
            .DependsOn(RunManagedUnitTests)
            .DependsOn(CompileDependencyLibs)
            .DependsOn(CompileFrameworkReproductions);

    Target LinuxFullCiBuild => _ =>
        _
            .Description("Convenience method for running the same build steps as the full Windows CI build")
            .DependsOn(Clean)
            .DependsOn(RestoreDotNet)
            .DependsOn(CompileManagedSrc)
            .DependsOn(CompileNativeSrcLinux)
            .DependsOn(BuildTracerHome)
            // .DependsOn(ZipTracerHome)
            .DependsOn(CompileManagedUnitTests)
            .DependsOn(RunManagedUnitTests);

    /// <summary>  
    /// Run the default build 
    /// </summary> 
    public static int Main() => Execute<Build>(x => x.LocalBuild);
}
