using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.IO;
using System.Linq;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MSBuild;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;

partial class Build
{
    Target CompileNativeLoader => _ => _
        .Unlisted()
        .Description("Compiles the native loader")
        .DependsOn(CompileNativeLoaderWindows)
        .DependsOn(CompileNativeLoaderLinux)
        .DependsOn(CompileNativeLoaderOsx);

    Target CompileNativeLoaderWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // If we're building for x64, build for x86 too
            var platforms =
                Equals(TargetPlatform, MSBuildTargetPlatform.x64)
                    ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                    : new[] { MSBuildTargetPlatform.x86 };

            // Can't use dotnet msbuild, as needs to use the VS version of MSBuild
            // Build native profiler assets
            MSBuild(s => s
                .SetTargetPath(NativeLoaderProject)
                .SetConfiguration(BuildConfiguration)
                .SetMSBuildPath()
                .DisableRestore()
                .SetMaxCpuCount(null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target CompileNativeLoaderLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var buildDirectory = NativeLoaderProject.Directory;

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -S .",
                workingDirectory: buildDirectory);
            CMake.Value(
                arguments: $"--build . --parallel",
                workingDirectory: buildDirectory);
        });

    Target CompileNativeLoaderOsx => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsOsx)
        .Executes(() =>
        {
            var buildDirectory = NativeLoaderProject.Directory;
            CMake.Value(arguments: ".", workingDirectory: buildDirectory);
            Make.Value(workingDirectory: buildDirectory);
        });

    Target PublishNativeLoader => _ => _
        .Unlisted()
        .DependsOn(PublishNativeLoaderWindows)
        .DependsOn(PublishNativeLoaderLinux)
        .DependsOn(PublishNativeLoaderOsx);

    Target PublishNativeLoaderWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatform)
            {
                // Copy native tracer assets
                var source = NativeProfilerProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             $"{NativeProfilerProject.Name}.dll";
                var dest = MonitoringHomeDirectory / $"win-{architecture}";
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                // Copy native loader assets
                source = NativeLoaderProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             "loader.conf";
                dest = MonitoringHomeDirectory;
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                source = NativeLoaderProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             $"{NativeLoaderProject.Name}.dll";
                var destFile = MonitoringHomeDirectory / $"Datadog.AutoInstrumentation.NativeLoader.{architecture.ToString()}.dll";
                CopyFile(source, destFile, FileExistsPolicy.Overwrite);

                source = NativeLoaderProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             $"{NativeLoaderProject.Name}.pdb";
                destFile = SymbolsDirectory / $"win-{architecture}";
                CopyFileToDirectory(source, destFile, FileExistsPolicy.Overwrite);
            }
        });

    Target PublishNativeLoaderLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
                // Copy native loader assets
                var source = NativeLoaderProject.Directory / "bin" / "loader.conf";
                var dest = MonitoringHomeDirectory;
                Logger.Info($"Copying '{source}' to '{dest}'");
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                source = NativeLoaderProject.Directory / "bin" /
                             $"{NativeLoaderProject.Name}.so";
                dest = MonitoringHomeDirectory;
                Logger.Info($"Copying file '{source}' to 'file {dest}'");
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
        });

    Target PublishNativeLoaderOsx=> _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsOsx)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
                // Copy native loader assets
                var source = NativeLoaderProject.Directory / "bin" / "loader.conf";
                var dest = MonitoringHomeDirectory;
                Logger.Info($"Copying '{source}' to '{dest}'");
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                source = NativeLoaderProject.Directory / "bin" /
                             $"{NativeLoaderProject.Name}.dylib";
                dest = MonitoringHomeDirectory;
                Logger.Info($"Copying file '{source}' to 'file {dest}'");
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
        });

}
