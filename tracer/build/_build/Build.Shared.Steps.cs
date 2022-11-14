using System;
using System.Collections.Generic;
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
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory}");
            CMake.Value(
                arguments: $"--build . --parallel --target {FileNames.NativeLoader}",
                workingDirectory: NativeBuildDirectory);
        });

    Target CompileNativeLoaderOsx => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsOsx)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            var extension = "dylib";
            foreach (var arch in OsxArchs)
            {
                DeleteDirectory(NativeBuildDirectory);

                var envVariables = new Dictionary<string, string> { ["CMAKE_OSX_ARCHITECTURES"] = arch };
                
                // Build native
                CMake.Value(
                    arguments: $"-B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE=Release",
                    environmentVariables: envVariables);
                CMake.Value(
                    arguments: $"--build {NativeBuildDirectory} --parallel --target {FileNames.NativeLoader}",
                    environmentVariables: envVariables);

                var sourceFile = NativeLoaderProject.Directory / "bin" / $"{NativeLoaderProject.Name}.{extension}";
                var destFile = NativeLoaderProject.Directory / "bin" / $"{NativeLoaderProject.Name}.{arch}.{extension}";

                // Check the architecture of the build
                var output = Lipo.Value(arguments: $"-archs {sourceFile}", logOutput: false);
                var strOutput = string.Join('\n', output.Where(o => o.Type == OutputType.Std).Select(o => o.Text));
                if (!strOutput.Contains(arch, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ApplicationException($"Invalid architecture, expected: '{arch}', actual: '{strOutput}'");
                }
                
                // Copy binary to the temporal destination
                CopyFile(sourceFile, destFile, FileExistsPolicy.Overwrite);
            }
        });

    Target PublishNativeLoader => _ => _
        .Unlisted()
        .DependsOn(PublishNativeLoaderWindows)
        .DependsOn(PublishNativeLoaderUnix)
        .DependsOn(PublishNativeLoaderOsx);

    Target PublishNativeLoaderWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatform)
            {
                var archFolder = $"win-{architecture}";

                // Copy native loader assets
                var source = NativeLoaderProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             "loader.conf";
                var dest = MonitoringHomeDirectory / archFolder;
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                source = NativeLoaderProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             $"{NativeLoaderProject.Name}.dll";
                dest = MonitoringHomeDirectory / archFolder;
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                source = NativeLoaderProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             $"{NativeLoaderProject.Name}.pdb";
                dest = SymbolsDirectory / archFolder;
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
            }
        });

    Target PublishNativeLoaderUnix => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
            // Copy native loader assets
            var (arch, ext) = GetUnixArchitectureAndExtension();
            var source = NativeLoaderProject.Directory / "bin" / "loader.conf";
            var dest = MonitoringHomeDirectory / arch;
            CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

            source = NativeLoaderProject.Directory / "bin" / $"{NativeLoaderProject.Name}.{ext}";
            dest = MonitoringHomeDirectory / arch;
            CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
        });

    Target PublishNativeLoaderOsx => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsOsx)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
            // Copy loader.conf
            var source = NativeLoaderProject.Directory / "bin" / "loader.conf";
            var dest = MonitoringHomeDirectory / "osx";
            CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
            
            // Create universal shared library with all archs in a single file
            var lstNativeBinaries = OsxArchs
               .Select(arch => NativeLoaderProject.Directory / "bin" / $"{NativeLoaderProject.Name}.{arch}.dylib");

            var destination = $"./{NativeLoaderProject.Name}.dylib";
            var destFolder = MonitoringHomeDirectory / "osx";
            Console.WriteLine($"Creating universal binary for {destination}");
            EnsureExistingDirectory(destFolder);
            var strNativeBinaries = string.Join(' ', lstNativeBinaries);
            Lipo.Value(arguments: $"{strNativeBinaries} -create -output {destFolder}/{destination}");
        });
}
