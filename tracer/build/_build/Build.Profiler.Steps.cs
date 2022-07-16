using System;
using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MSBuild;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using Nuke.Common.Tools.NuGet;

partial class Build
{
    Target CompileProfilerNativeSrc => _ => _
        .Unlisted()
        .Description("Compiles the native profiler assets")
        .DependsOn(CompileProfilerNativeSrcWindows)
        .DependsOn(CompileProfilerNativeSrcAndTestLinux);

    Target CompileProfilerNativeSrcWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            var project = ProfilerDirectory.GlobFiles("**/Datadog.Profiler.Native.Windows.vcxproj").Single();

            var nugetPackageRestoreDirectory = RootDirectory / "packages";

            NuGetTasks.NuGetRestore(s => s
                .SetTargetPath(project)
                .SetVerbosity(NuGetVerbosity.Normal)
                .SetPackagesDirectory(nugetPackageRestoreDirectory));

            // If we're building for x64, build for x86 too
            var platforms =
                Equals(TargetPlatform, MSBuildTargetPlatform.x64)
                    ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                    : new[] { MSBuildTargetPlatform.x86 };

            // Can't use dotnet msbuild, as needs to use the VS version of MSBuild
            // Build native profiler assets
            MSBuild(s => s
                .SetTargetPath(project)
                .SetConfiguration(BuildConfiguration)
                .SetProperty("SpectreMitigation", "false") // Enforce the same build in all CI environments
                .SetMSBuildPath()
                .DisableRestore()
                .SetMaxCpuCount(null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target CompileProfilerNativeSrcAndTestLinux => _ => _
        .Unlisted()
        .Description("Compile Profiler native code")
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerLinuxBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {ProfilerLinuxBuildDirectory} -S {ProfilerDirectory} -DCMAKE_BUILD_TYPE=Release");

            CMake.Value(
                arguments: $"--build {ProfilerLinuxBuildDirectory} --parallel");

            if (IsAlpine)
            {
                // On Alpine, we do not have permission to access the file libunwind-prefix/src/libunwind/config/config.guess
                // Make the whole folder and its content accessible by everyone to make sure the upload process does not fail
                Chmod.Value.Invoke(" -R 777 " + ProfilerLinuxBuildDirectory);
            }
        });

    Target RunProfilerNativeUnitTestsLinux => _ => _
        .Unlisted()
        .Description("Run profiler native unit tests")
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileProfilerNativeSrcAndTestLinux)
        .Executes(() =>
        {
            var workingDirectory = ProfilerOutputDirectory / "bin" / "Datadog.Profiler.Native.Tests";
            EnsureExistingDirectory(workingDirectory);

            var exePath = workingDirectory / "Datadog.Profiler.Native.Tests";
            Chmod.Value.Invoke("+x " + exePath);

            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe("--gtest_output=xml", workingDirectory: workingDirectory);
        });

    Target CompileProfilerNativeTestsWindows => _ => _
        .Unlisted()
        .After(CompileProfilerNativeSrc)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // If we're building for x64, build for x86 too
            var platforms =
                Equals(TargetPlatform, MSBuildTargetPlatform.x64)
                    ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                    : new[] { MSBuildTargetPlatform.x86 };

            // Can't use dotnet msbuild, as needs to use the VS version of MSBuild
            MSBuild(s => s
                .SetTargetPath(ProfilerMsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetMSBuildPath()
                .SetTargets("BuildCppTests")
                .DisableRestore()
                .SetMaxCpuCount(null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target RunProfilerNativeUnitTestsWindows => _ => _
        .Unlisted()
        .After(CompileProfilerNativeTestsWindows)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            var configAndTarget = $"{BuildConfiguration}-{TargetPlatform}";
            var workingDirectory = ProfilerOutputDirectory / "bin" / configAndTarget / "profiler" / "test" / "Datadog.Profiler.Native.Tests";
            EnsureExistingDirectory(workingDirectory);

            var exePath = workingDirectory / "Datadog.Profiler.Native.Tests.exe";
            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe("--gtest_output=xml", workingDirectory: workingDirectory);
        });


    Target PublishProfiler => _ => _
        .Unlisted()
        .DependsOn(PublishProfilerWindows)
        .DependsOn(PublishProfilerLinux);

    Target PublishProfilerLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileProfilerNativeSrc)
        .Executes(() =>
        {
            var source = ProfilerOutputDirectory / "DDProf-Deploy" / "Datadog.AutoInstrumentation.Profiler.Native.x64.so";
            var dest = ProfilerHomeDirectory;
            Logger.Info($"Copying file '{source}' to 'file {dest}'");
            CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

            source = ProfilerOutputDirectory / "DDProf-Deploy" / "Datadog.Linux.ApiWrapper.x64.so";
            dest = ProfilerHomeDirectory;
            Logger.Info($"Copying file '{source}' to 'file {dest}'");
            CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
        });

    Target PublishProfilerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileProfilerNativeSrc)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatform)
            {
                var source = ProfilerOutputDirectory / "DDProf-Deploy" / $"Datadog.AutoInstrumentation.Profiler.Native.{architecture}.dll";
                var dest = ProfilerHomeDirectory;
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                source = ProfilerOutputDirectory / "DDProf-Deploy" / $"Datadog.AutoInstrumentation.Profiler.Native.{architecture}.pdb";
                dest = SymbolsDirectory / $"win-{architecture}" / Path.GetFileName(source);
                CopyFile(source, dest, FileExistsPolicy.Overwrite);
            }
        });

    Target BuildAndRunProfilerLinuxIntegrationTests => _ => _
        .Requires(() => IsLinux && !IsArm64)
        .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader, ZipMonitoringHome)
        .Description("Builds and runs the profiler linux integration tests.")
        .DependsOn(BuildProfilerLinuxIntegrationTests)
        .DependsOn(RunProfilerLinuxIntegrationTests);

    Target BuildProfilerLinuxIntegrationTests => _ => _
        .Description("Builds the profiler linux integration tests.")
        .Requires(() => IsLinux && !IsArm64)
        .DependsOn(CompileProfilerSamplesLinux)
        .DependsOn(CompileProfilerLinuxIntegrationTests);

    Target CompileProfilerLinuxIntegrationTests => _ => _
        .Unlisted()
        .After(PublishProfilerLinux)
        .After(CompileProfilerSamplesLinux)
        .Executes(() =>
        {
            // Build the actual integration test projects for x64
            var integrationTestProjects = ProfilerDirectory.GlobFiles("test/*.IntegrationTests/*.csproj");
            DotNetBuild(x => x
                    // .EnableNoRestore()
                    .EnableNoDependencies()
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatform(MSBuildTargetPlatform.x64)
                    .SetNoWarnDotNetCore3()
                    .When(TestAllPackageVersions, o => o.SetProperty("TestAllPackageVersions", "true"))
                    .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                        o.SetPackageDirectory(NugetPackageDirectory))
                    .CombineWith(integrationTestProjects, (c, project) => c
                        .SetProjectFile(project)));
        });


    Target CompileProfilerSamplesLinux => _ => _
        .Unlisted()
        .Executes(() =>
        {
            var samplesToBuild = ProfilerSamplesSolution.GetProjects("*");

            // Always x64
            DotNetBuild(x => x
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatform(MSBuildTargetPlatform.x64)
                    .SetNoWarnDotNetCore3()
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
                    .CombineWith(samplesToBuild, (c, project) => c
                        .SetProjectFile(project)));
        });

    Target RunProfilerLinuxIntegrationTests => _ => _
        .After(CompileProfilerSamplesLinux)
        .After(CompileProfilerLinuxIntegrationTests)
        .Description("Runs the profiler linux integration tests")
        .Requires(() => IsLinux && !IsArm64)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerTestLogsDirectory);

            var integrationTestProjects = ProfilerDirectory.GlobFiles("test/*.IntegrationTests/*.csproj")
                .Select(x => ProfilerSolution.GetProject(x))
                .ToList();

            try
            {
                // Run these ones in parallel
                // Always x64
                DotNetTest(config => config
                        .SetConfiguration(BuildConfiguration)
                        .SetTargetPlatform(MSBuildTargetPlatform.x64)
                        .EnableNoRestore()
                        .EnableNoBuild()
                        .SetProcessEnvironmentVariable("DD_TESTING_OUPUT_DIR", ProfilerBuildDataDirectory)
                        .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                        .CombineWith(integrationTestProjects, (s, project) => s
                            .EnableTrxLogOutput(ProfilerBuildDataDirectory / "results" / project.Name)
                            .SetProjectFile(project)),
                    degreeOfParallelism: 2);
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });
}
