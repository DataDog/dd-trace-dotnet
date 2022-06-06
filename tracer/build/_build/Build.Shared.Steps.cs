using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.IO;
using System.Linq;
using Microsoft.Build.Tasks;
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
        .After(CompileProfilerManagedSrc)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var buildDirectory = NativeLoaderProject.Directory;

            CMake.Value(
                arguments: $"-S .",
                workingDirectory: buildDirectory);
            CMake.Value(
                arguments: $"--build . --parallel",
                workingDirectory: buildDirectory);
        });

    Target CompileNativeLoaderOsx => _ => _
        .Unlisted()
        .After(CompileProfilerManagedSrc)
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
        .DependsOn(PublishNativeLoaderLinux);

    Target PublishNativeLoaderWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatform)
            {
                var dest = MonitoringHomeDirectory / $"win-{architecture}";
                var symbolsDest = SymbolsDirectory / $"win-{architecture}";

                var nativeLoaderBuildDir = NativeLoaderProject.Directory / "bin" / BuildConfiguration / architecture.ToString();
                CopyNativeLoaderAssets(dest, symbolsDest, nativeLoaderBuildDir, "dll");
            }
        });

    Target PublishNativeLoaderLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux || IsOsx)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
            var (arch, ext) = GetUnixArchitectureAndExtension();

            var dest = MonitoringHomeDirectory / arch;
            var symbolsDest = SymbolsDirectory / arch;

            var nativeLoaderBuildDir = NativeLoaderProject.Directory / "bin";

            CopyNativeLoaderAssets(dest, symbolsDest, nativeLoaderBuildDir, ext);
        });

    void CopyNativeLoaderAssets(
        AbsolutePath destination,
        AbsolutePath symbolsDestination,
        AbsolutePath nativeLoaderBuildDir,
        string fileExtension)
    {
        var loaderConf = nativeLoaderBuildDir / Constants.LoaderConfFilename;
        CopyFileToDirectory(loaderConf, destination, FileExistsPolicy.Overwrite);

        var nativeLoader  = nativeLoaderBuildDir / $"{NativeLoaderProject.Name}.{fileExtension}";
        var nativeLoaderDest = destination / $"{Constants.NativeLoaderFilename}.{fileExtension}";
        CopyFile(nativeLoader, nativeLoaderDest, FileExistsPolicy.Overwrite);

        if (IsWin)
        {
            var nativeLoaderPdb = nativeLoaderBuildDir / $"{NativeLoaderProject.Name}.pdb";
            var nativeLoaderPdbDest = symbolsDestination / $"{Constants.NativeLoaderFilename}.pdb";
            CopyFile(nativeLoaderPdb, nativeLoaderPdbDest, FileExistsPolicy.Overwrite);
        }
    }

}
