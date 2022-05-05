using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MSBuild;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using Nuke.Common.Tools.NuGet;

partial class Build
{
    Target CompileProfilerManagedSrc => _ => _
        .Unlisted()
        .Description("Compiles the managed code in the src directory")
        .After(CreateRequiredDirectories)
        .After(Restore)
        .Executes(() =>
        {
            List<string> projects = new();
            projects.Add(SharedDirectory.GlobFiles("**/Datadog.AutoInstrumentation.ManagedLoader.csproj").Single());
            projects.Add(ProfilerDirectory.GlobFiles("**/Datadog.Profiler.Managed.csproj").Single());

            // Build the shared managed loader
            DotNetBuild(s => s
                .SetTargetPlatformAnyCPU()
                .SetConfiguration(BuildConfiguration)
                .CombineWith(projects, (x, project) => x
                    .SetProjectFile(project)));
        });

    Target CompileProfilerNativeSrc => _ => _
        .Unlisted()
        .Description("Compiles the native profiler assets")
        .DependsOn(CompileProfilerNativeSrcWindows)
        .DependsOn(CompileProfilerNativeSrcLinux);

    Target CompileProfilerNativeSrcWindows => _ => _
        .Unlisted()
        .After(CompileProfilerManagedSrc) // Keeping this because this may depend on embedding managed libs
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

    Target PrepareProfilerBuildFolderLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var buildDirectory = ProfilerBuildDirectory / "cmake";
            EnsureExistingDirectory(buildDirectory);

            CMake.Value(
                arguments: $"-S {ProfilerDirectory}",
                workingDirectory: buildDirectory);

        });

    Target CompileProfilerNativeSrcLinux => _ => _
        .Unlisted()
        .Description("Compile Profiler native code")
        .DependsOn(PrepareProfilerBuildFolderLinux)
        .After(CompileProfilerManagedSrc)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var buildDirectory = ProfilerBuildDirectory / "cmake";
            EnsureExistingDirectory(buildDirectory);

            Make.Value(
                arguments: "Datadog.AutoInstrumentation.Profiler.Native.x64",
                workingDirectory: buildDirectory);

            if (IsAlpine)
            {
                // On Alpine, we do have permission to access the file libunwind-prefix/src/libunwind/config/config.guess
                // Make the whole folder and its content accessible by everyone to make sure the upload process does not fail
                Chmod.Value.Invoke(" -R 777 " + buildDirectory);
            }
        });

    Target CompileProfilerNativeTestsLinux => _ => _
        .Unlisted()
        .DependsOn(PrepareProfilerBuildFolderLinux)
        .After(CompileProfilerManagedSrc) // This dependency is needed today but will be remove soon
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var buildDirectory = ProfilerBuildDirectory / "cmake";
            EnsureExistingDirectory(buildDirectory);

            Make.Value(
                arguments: "Datadog.Profiler.Native.Tests",
                workingDirectory: buildDirectory);
        });

    Target RunProfilerNativeUnitTestsWindows => _ => _
        .Unlisted()
        .After(CompileProfilerNativeSrcWindows)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            var configAndTarget = $"{BuildConfiguration}-{TargetPlatform}";
            var workingDirectory = ProfilerBuildDirectory / "bin" / configAndTarget / "profiler" / "test" / "Datadog.Profiler.Native.Tests";
            EnsureExistingDirectory(workingDirectory);


            var exePath = workingDirectory / "Datadog.Profiler.Native.Tests.exe";
            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe("--gtest_output=xml", workingDirectory: workingDirectory);

        });

    Target RunProfilerNativeUnitTestsLinux => _ => _
        .Unlisted()
        .Description("Run profiler native unit tests")
        .After(CompileProfilerNativeTestsLinux)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var workingDirectory = ProfilerBuildDirectory / "bin" / "Datadog.Profiler.Native.Tests";
            EnsureExistingDirectory(workingDirectory);

            var exePath = workingDirectory / "Datadog.Profiler.Native.Tests";
            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe("--gtest_output=xml", workingDirectory: workingDirectory);
        });

}
