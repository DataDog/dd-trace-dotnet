using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static CustomDotNetTasks;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;

// #pragma warning disable SA1306
// #pragma warning disable SA1134
// #pragma warning disable SA1111
// #pragma warning disable SA1400
// #pragma warning disable SA1401

partial class Build
{
    [Solution("Datadog.Trace.sln")] readonly Solution Solution;
    AbsolutePath TracerDirectory => RootDirectory / "tracer";
    AbsolutePath SharedDirectory => RootDirectory / "shared";
    AbsolutePath ProfilerDirectory => RootDirectory / "profiler";
    AbsolutePath MsBuildProject => TracerDirectory / "Datadog.Trace.proj";

    AbsolutePath OutputDirectory => TracerDirectory / "bin";
    AbsolutePath SymbolsDirectory => OutputDirectory / "symbols";
    AbsolutePath DDTracerHomeDirectory => DDTracerHome ?? (OutputDirectory / "dd-tracer-home");
    AbsolutePath ArtifactsDirectory => Artifacts ?? (OutputDirectory / "artifacts");
    AbsolutePath WindowsTracerHomeZip => ArtifactsDirectory / "windows-tracer-home.zip";
    AbsolutePath WindowsSymbolsZip => ArtifactsDirectory / "windows-native-symbols.zip";
    AbsolutePath BuildDataDirectory => TracerDirectory / "build_data";
    AbsolutePath TestLogsDirectory => BuildDataDirectory / "logs";
    AbsolutePath ToolSourceDirectory => ToolSource ?? (OutputDirectory / "runnerTool");
    AbsolutePath ToolInstallDirectory => ToolDestination ?? (ToolSourceDirectory / "install");

    AbsolutePath MonitoringHomeDirectory => MonitoringHome ?? (SharedDirectory / "bin" / "monitoring-home");

    [Solution("profiler/src/Demos/Datadog.Demos.sln")] readonly Solution ProfilerSamplesSolution;
    [Solution("Datadog.Profiler.sln")] readonly Solution ProfilerSolution;
    AbsolutePath ProfilerMsBuildProject => ProfilerDirectory / "src" / "ProfilerEngine" / "Datadog.Profiler.Native.Windows" / "Datadog.Profiler.Native.Windows.WithTests.proj";
    AbsolutePath ProfilerOutputDirectory => RootDirectory / "profiler" / "_build";
    AbsolutePath ProfilerLinuxBuildDirectory => ProfilerOutputDirectory / "cmake";
    AbsolutePath ProfilerBuildDataDirectory => ProfilerDirectory / "build_data";
    AbsolutePath ProfilerTestLogsDirectory => ProfilerBuildDataDirectory / "logs";

    const string LibDdwafVersion = "1.3.0";
    AbsolutePath LibDdwafDirectory => (NugetPackageDirectory ?? RootDirectory / "packages") / $"libddwaf.{LibDdwafVersion}";

    AbsolutePath SourceDirectory => TracerDirectory / "src";
    AbsolutePath BuildDirectory => TracerDirectory / "build";
    AbsolutePath TestsDirectory => TracerDirectory / "test";
    AbsolutePath DistributionHomeDirectory => Solution.GetProject(Projects.DatadogMonitoringDistribution).Directory / "home";


    AbsolutePath TempDirectory => (AbsolutePath)(IsWin ? Path.GetTempPath() : "/tmp/");

    readonly string[] WafWindowsArchitectureFolders =
    {
        "win-x86", "win-x64"
    };
    Project NativeProfilerProject => Solution.GetProject(Projects.ClrProfilerNative);
    Project NativeLoaderProject => Solution.GetProject(Projects.NativeLoader);

    [LazyPathExecutable(name: "cmake")] readonly Lazy<Tool> CMake;
    [LazyPathExecutable(name: "make")] readonly Lazy<Tool> Make;
    [LazyPathExecutable(name: "fpm")] readonly Lazy<Tool> Fpm;
    [LazyPathExecutable(name: "gzip")] readonly Lazy<Tool> GZip;
    [LazyPathExecutable(name: "cmd")] readonly Lazy<Tool> Cmd;
    [LazyPathExecutable(name: "chmod")] readonly Lazy<Tool> Chmod;
    [LazyPathExecutable(name: "objcopy")] readonly Lazy<Tool> ExtractDebugInfo;
    [LazyPathExecutable(name: "strip")] readonly Lazy<Tool> StripBinary;

    IEnumerable<MSBuildTargetPlatform> ArchitecturesForPlatform =>
        Equals(TargetPlatform, MSBuildTargetPlatform.x64)
            ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
            : new[] { MSBuildTargetPlatform.x86 };

    bool IsArm64 => RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    string LinuxArchitectureIdentifier => IsArm64 ? "arm64" : TargetPlatform.ToString();

    IEnumerable<string> LinuxPackageTypes => IsAlpine ? new[] { "tar" } : new[] { "deb", "rpm", "tar" };

    IEnumerable<Project> ProjectsToPack => new[]
    {
        Solution.GetProject(Projects.DatadogTrace),
        Solution.GetProject(Projects.DatadogTraceOpenTracing),
        Solution.GetProject(Projects.DatadogTraceAnnotations),
    };

    Project[] ParallelIntegrationTests => new[]
    {
        Solution.GetProject(Projects.TraceIntegrationTests),
        Solution.GetProject(Projects.OpenTracingIntegrationTests),
    };

    Project[] ClrProfilerIntegrationTests => new[]
    {
        Solution.GetProject(Projects.ClrProfilerIntegrationTests),
        Solution.GetProject(Projects.AppSecIntegrationTests),
        Solution.GetProject(Projects.ToolIntegrationTests)
    };

    readonly IEnumerable<TargetFramework> TargetFrameworks = new[]
    {
        TargetFramework.NET461,
        TargetFramework.NETSTANDARD2_0,
        TargetFramework.NETCOREAPP3_1,
        TargetFramework.NET6_0,
    };

    Target CreateRequiredDirectories => _ => _
        .Unlisted()
        .Executes(() =>
        {
            EnsureExistingDirectory(MonitoringHomeDirectory);
            EnsureExistingDirectory(ArtifactsDirectory);
            EnsureExistingDirectory(DDTracerHomeDirectory);
            EnsureExistingDirectory(BuildDataDirectory);
        });

    Target Restore => _ => _
        .After(Clean)
        .Unlisted()
        .Executes(() =>
        {
            if (IsWin)
            {
                NuGetTasks.NuGetRestore(s => s
                    .SetTargetPath(Solution)
                    .SetVerbosity(NuGetVerbosity.Normal)
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                        o.SetPackagesDirectory(NugetPackageDirectory)));
            }
            else
            {
                DotNetRestore(s => s
                    .SetProjectFile(Solution)
                    .SetVerbosity(DotNetVerbosity.Normal)
                    // .SetTargetPlatform(Platform) // necessary to ensure we restore every project
                    .SetProperty("configuration", BuildConfiguration.ToString())
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                        o.SetPackageDirectory(NugetPackageDirectory)));
            }
        });

    Target CompileNativeSrcWindows => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // If we're building for x64, build for x86 too
            var platforms =
                Equals(TargetPlatform, MSBuildTargetPlatform.x64)
                    ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                    : new[] { MSBuildTargetPlatform.x86 };

            // Can't use dotnet msbuild, as needs to use the VS version of MSBuild
            // Build native tracer assets
            MSBuild(s => s
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetMSBuildPath()
                .SetTargets("BuildCppSrc")
                .DisableRestore()
                .SetMaxCpuCount(null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target CompileNativeSrcLinux => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var buildDirectory = NativeProfilerProject.Directory / "build";
            EnsureExistingDirectory(buildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {buildDirectory} -S {NativeProfilerProject.Directory} -DCMAKE_BUILD_TYPE=Release");
            CMake.Value(
                arguments: $"--build {buildDirectory} --parallel");
        });

    Target CompileNativeSrcMacOs => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .OnlyWhenStatic(() => IsOsx)
        .Executes(() =>
        {
            var nativeProjectDirectory = NativeProfilerProject.Directory;
            CMake.Value(arguments: ".", workingDirectory: nativeProjectDirectory);
            Make.Value(workingDirectory: nativeProjectDirectory);
        });

    Target CompileNativeSrc => _ => _
        .Unlisted()
        .Description("Compiles the native loader")
        .DependsOn(CompileNativeSrcWindows)
        .DependsOn(CompileNativeSrcMacOs)
        .DependsOn(CompileNativeSrcLinux);

    Target CompileManagedSrc => _ => _
        .Unlisted()
        .Description("Compiles the managed code in the src directory")
        .After(CreateRequiredDirectories)
        .After(Restore)
        .Executes(() =>
        {
            // Always AnyCPU
            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetTargetPlatformAnyCPU()
                .SetConfiguration(BuildConfiguration)
                .DisableRestore()
                .SetTargets("BuildCsharpSrc")
            );
        });


    Target CompileNativeTestsWindows => _ => _
        .Unlisted()
        .After(CompileNativeSrc)
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
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetMSBuildPath()
                .SetTargets("BuildCppTests")
                .DisableRestore()
                .SetMaxCpuCount(null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target CompileNativeTestsLinux => _ => _
        .Unlisted()
        .After(CompileNativeSrc)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            Logger.Error("We don't currently run unit tests on Linux");
        });

    Target CompileNativeTests => _ => _
        .Unlisted()
        .Description("Compiles the native unit tests (native loader, profiler)")
        .DependsOn(CompileNativeTestsWindows)
        .DependsOn(CompileNativeTestsLinux)
        .DependsOn(CompileProfilerNativeTestsWindows);

    Target DownloadLibDdwaf => _ => _
        .Unlisted()
        .After(CreateRequiredDirectories)
        .Executes(async () =>
        {
            var libDdwafUri = new Uri($"https://www.nuget.org/api/v2/package/libddwaf/{LibDdwafVersion}");
            var libDdwafZip = TempDirectory / "libddwaf.zip";

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(libDdwafUri);

                response.EnsureSuccessStatusCode();

                await using var file = File.Create(libDdwafZip);
                await using var stream = await response.Content.ReadAsStreamAsync();
                await stream.CopyToAsync(file);
            }

            Console.WriteLine($"{libDdwafZip} downloaded. Extracting to {LibDdwafDirectory}...");

            UncompressZip(libDdwafZip, LibDdwafDirectory);
        });

    Target CopyLibDdwaf => _ => _
        .Unlisted()
        .After(Clean)
        .After(DownloadLibDdwaf)
        .Executes(() =>
        {
            if (IsWin)
            {
                foreach (var architecture in WafWindowsArchitectureFolders)
                {
                    var source = LibDdwafDirectory / "runtimes" / architecture / "native" / "ddwaf.dll";
                    var dest = MonitoringHomeDirectory / architecture;
                    CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
                }
            }
            else
            {
                var (architecture, ext) = GetUnixArchitectureAndExtension();
                var ddwafFileName = $"libddwaf.{ext}";

                var source = LibDdwafDirectory / "runtimes" / architecture / "native" / ddwafFileName;
                var dest = MonitoringHomeDirectory;
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

            }
        });

    Target CopyLibDdwafForAppSecUnitTests => _ => _
        .Unlisted()
        .After(Clean)
        .After(DownloadLibDdwaf)
        .Executes(() =>
        {
            var project = Solution.GetProject(Projects.AppSecUnitTests);
            CopyWafForProject(project);
        });

    private void CopyWafForProject(Project project)
    {
        var directory = project.Directory;
        var targetFrameworks = project.GetTargetFrameworks();
        if (IsWin)
        {
            foreach (var architecture in WafWindowsArchitectureFolders)
            {
                CopyWaf(architecture, targetFrameworks, directory, "ddwaf", "dll");
            }
        }
        else
        {
            var (architecture, ext) = GetUnixArchitectureAndExtension();
            CopyWaf(architecture, targetFrameworks, directory, "libddwaf", ext);
        }

        void CopyWaf(string architecture, IEnumerable<string> frameworks, AbsolutePath absolutePath, string wafFileName, string extension)
        {
            var source = LibDdwafDirectory / "runtimes" / architecture / "native" / $"{wafFileName}.{extension}";
            foreach (var fmk in frameworks)
            {
                var targetDir = absolutePath / "bin" / BuildConfiguration / fmk;
                var archTargetDir = targetDir / architecture;
                CopyFileToDirectory(source, archTargetDir, FileExistsPolicy.Overwrite);
                if (!IsWin)
                {
                    var tracerNativeLib = DDTracerHomeDirectory / architecture / $"Datadog.Tracer.Native.{extension}";
                    // The tests use the "wrong" name for the library currently
                    var target = targetDir / $"Datadog.Trace.ClrProfiler.Native.{extension}";
                    CopyFile(tracerNativeLib, target, FileExistsPolicy.Overwrite);
                }
            }
        }
    }

    Target PublishManagedProfiler => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .Executes(() =>
        {
            var targetFrameworks = IsWin
                ? TargetFrameworks
                : TargetFrameworks.Where(framework => !framework.ToString().StartsWith("net4"));

            // Publish Datadog.Trace.MSBuild which includes Datadog.Trace and Datadog.Trace.AspNet
            DotNetPublish(s => s
                .SetProject(Solution.GetProject(Projects.DatadogTraceMsBuild))
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatformAnyCPU()
                .EnableNoBuild()
                .EnableNoRestore()
                .CombineWith(targetFrameworks, (p, framework) => p
                    .SetFramework(framework)
                    .SetOutput(MonitoringHomeDirectory / framework)));
        });

    Target PublishNativeSymbolsWindows => _ => _
      .Unlisted()
      .OnlyWhenStatic(() => IsWin)
      .After(CompileNativeSrc, PublishManagedProfiler)
      .Executes(() =>
       {
           foreach (var architecture in ArchitecturesForPlatform)
           {
               var source = NativeProfilerProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                            $"{NativeProfilerProject.Name}.pdb";
               var dest = SymbolsDirectory / $"win-{architecture}" / Path.GetFileName(source);
               CopyFile(source, dest, FileExistsPolicy.Overwrite);
           }
       });

    Target PublishNativeProfilerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileNativeSrc, PublishManagedProfiler)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatform)
            {
                // Copy native tracer assets
                var source = NativeProfilerProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             $"{NativeProfilerProject.Name}.dll";
                var dest = MonitoringHomeDirectory / $"win-{architecture}";
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
            }
        });

    Target PublishNativeProfilerLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileNativeSrc, PublishManagedProfiler)
        .Executes(() =>
        {
            // copy createLogPath.sh
            CopyFileToDirectory(
                BuildDirectory / "artifacts" / "createLogPath.sh",
                MonitoringHomeDirectory,
                FileExistsPolicy.Overwrite);

            // Copy Native file
            CopyFileToDirectory(
                NativeProfilerProject.Directory / "build" / "bin" / $"{NativeProfilerProject.Name}.so",
                MonitoringHomeDirectory,
                FileExistsPolicy.Overwrite);
        });

    Target PublishNativeProfilerMacOs => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsOsx)
        .After(CompileNativeSrc, PublishManagedProfiler)
        .Executes(() =>
        {
            // copy createLogPath.sh
            CopyFileToDirectory(
                BuildDirectory / "artifacts" / "createLogPath.sh",
                MonitoringHomeDirectory,
                FileExistsPolicy.Overwrite);

            // Create home directory
            CopyFileToDirectory(
                NativeProfilerProject.Directory / "bin" / $"{NativeProfilerProject.Name}.dylib",
                MonitoringHomeDirectory,
                FileExistsPolicy.Overwrite);
        });

    Target PublishNativeProfiler => _ => _
        .Unlisted()
        .DependsOn(PublishNativeProfilerWindows)
        .DependsOn(PublishNativeProfilerLinux)
        .DependsOn(PublishNativeProfilerMacOs);

    Target CreateDdTracerHome => _ => _
       .Unlisted()
       .After(PublishNativeProfiler, PublishManagedProfiler, DownloadLibDdwaf, CopyLibDdwaf)
       .Executes(() =>
       {
           // start by copying everything from the tracer home dir
           CopyDirectoryRecursively(MonitoringHomeDirectory, DDTracerHomeDirectory, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);

           if (IsWin)
           {
               // windows already has the expected layout
               // except we _temporarily_ need to rename the Tracer native loader
               // As we're not using the shared native loader yet
               foreach (var arch in ArchitecturesForPlatform)
               {
                   MoveFile(
                       DDTracerHomeDirectory / $"win-{arch}" / $"{Projects.ClrProfilerNative}.dll",
                       DDTracerHomeDirectory / $"win-{arch}" / $"{Projects.NativeLoader}.dll");
               }
               return;
           }

           // Move the native file to the architecture-specific folder
           var (architecture, ext) = GetUnixArchitectureAndExtension();

           var profilerFileName = $"{NativeProfilerProject.Name}.{ext}";
           var ddwafFileName = $"libddwaf.{ext}";

           var outputDir = DDTracerHomeDirectory / architecture;

           EnsureCleanDirectory(outputDir);
           MoveFile(
               DDTracerHomeDirectory / profilerFileName,
               outputDir / profilerFileName);

           var srcDdwafFile = DDTracerHomeDirectory / ddwafFileName;
           MoveFile(
               srcDdwafFile,
               DDTracerHomeDirectory / architecture / ddwafFileName);
       });

    Target BuildMsi => _ => _
        .Unlisted()
        .Description("Builds the .msi files from the repo")
        .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            MSBuild(s => s
                    .SetTargetPath(SharedDirectory / "src" / "msi-installer" / "WindowsInstaller.wixproj")
                    .SetConfiguration(BuildConfiguration)
                    .SetMSBuildPath()
                    .AddProperty("RunWixToolsOutOfProc", true)
                    .SetProperty("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetMaxCpuCount(null)
                    .CombineWith(ArchitecturesForPlatform, (o, arch) => o
                        .SetProperty("MsiOutputPath", ArtifactsDirectory / arch.ToString())
                        .SetTargetPlatform(arch)),
                degreeOfParallelism: 2);
        });

    Target CreateDistributionHome => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .Executes(() =>
        {
            // Copy existing files from tracer home to the Distribution location
            // This is a hack as dd-tracer-home currently contains the correct filename
            CopyDirectoryRecursively(DDTracerHomeDirectory, DistributionHomeDirectory, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);

            // Ensure createLogPath.sh is copied to the directory
            CopyFileToDirectory(
                BuildDirectory / "artifacts" / "createLogPath.sh",
                DistributionHomeDirectory,
                FileExistsPolicy.Overwrite);
        });

    /// <summary>
    /// This target is a bit of a hack, but means that we actually use the All CPU builds in intgration tests etc
    /// </summary>
    Target CreatePlatformlessSymlinks => _ => _
        .Description("Copies the build output from 'All CPU' platforms to platform-specific folders")
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileManagedSrc)
        .After(CompileDependencyLibs)
        .After(CompileManagedTestHelpers)
        .After(BuildRunnerTool)
        .Executes(() =>
        {
            // create junction for each directory
            var directories = TracerDirectory.GlobDirectories(
                $"src/**/obj/{BuildConfiguration}",
                $"src/**/bin/{BuildConfiguration}",
                $"src/Datadog.Trace.Tools.Runner/obj/{BuildConfiguration}",
                $"src/Datadog.Trace.Tools.Runner/bin/{BuildConfiguration}",
                $"test/Datadog.Trace.TestHelpers/**/obj/{BuildConfiguration}",
                $"test/Datadog.Trace.TestHelpers/**/bin/{BuildConfiguration}",
                $"test/test-applications/integrations/dependency-libs/**/bin/{BuildConfiguration}"
            );

            directories.ForEach(existingDir =>
            {
                var newDir = existingDir.Parent / $"{TargetPlatform}" / BuildConfiguration;
                if (DirectoryExists(newDir))
                {
                    Logger.Info($"Skipping '{newDir}' as already exists");
                }
                else
                {
                    EnsureExistingDirectory(newDir.Parent);
                    Cmd.Value(arguments: $"cmd /c mklink /J \"{newDir}\" \"{existingDir}\"");
                }
            });
        });

    Target ZipSymbols => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .DependsOn(PublishNativeSymbolsWindows)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            CompressZip(SymbolsDirectory, WindowsSymbolsZip, fileMode: FileMode.Create);
        });

    Target ZipMonitoringHome => _ => _
        .Unlisted()
        .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader)
        .Requires(() => Version)
        .Executes(() =>
        {
            if (IsWin)
            {
                // This is a hack for now, as the dd-tracer-home folder contains the correct layout and files for windows 
                CompressZip(DDTracerHomeDirectory, WindowsTracerHomeZip, fileMode: FileMode.Create);
                // for now we do not need a monitoring-home.zip file. So no need to create it.
            }
            else if (IsLinux)
            {
                void ExtractDebugInfoAndStripSymbols()
                {
                    var files = MonitoringHomeDirectory.GlobFiles("**/*.so");

                    EnsureExistingDirectory(SymbolsDirectory);

                    foreach (var file in files)
                    {
                        var outputFile = SymbolsDirectory / Path.GetFileNameWithoutExtension(file);

                        Logger.Info($"Extracting debug symbol for {file} to {outputFile}.debug");
                        ExtractDebugInfo.Value(arguments: $"--only-keep-debug {file} {outputFile}.debug");

                        Logger.Info($"Stripping out unneeded information from {file}");
                        StripBinary.Value(arguments: $"--strip-unneeded {file}");
                    }
                }

                var fpm = Fpm.Value;
                var gzip = GZip.Value;
                var chmod = Chmod.Value;
                var packageName = "datadog-dotnet-apm";

                var workingDirectory = ArtifactsDirectory / $"linux-{LinuxArchitectureIdentifier}";
                EnsureCleanDirectory(workingDirectory);

                var tracerNativeFile = MonitoringHomeDirectory / "Datadog.Tracer.Native.so";
                var newTracerNativeFile = MonitoringHomeDirectory / "tracer" / "Datadog.Tracer.Native.so";
                MoveFile(tracerNativeFile, newTracerNativeFile);

                // somehow the permissions are lost along the way, ensure they are correctly set here
                var createLogPathScript = MonitoringHomeDirectory / "createLogPath.sh";
                chmod.Invoke("+x " + createLogPathScript);

                ExtractDebugInfoAndStripSymbols();

                foreach (var packageType in LinuxPackageTypes)
                {
                    var args = new List<string>()
                    {
                        "-f",
                        "-s dir",
                        $"-t {packageType}",
                        $"-n {packageName}",
                        $"-v {Version}",
                        packageType == "tar" ? "" : "--prefix /opt/datadog",
                        $"--chdir {MonitoringHomeDirectory}",
                        "createLogPath.sh",
                        "netstandard2.0/",
                        "netcoreapp3.1/",
                        "net6.0/",
                        "Datadog.Trace.ClrProfiler.Native.so",
                    };

                    args.Add("libddwaf.so");
                    args.Add("tracer/");
                    args.Add("continuousprofiler/");
                    args.Add("loader.conf");

                    var arguments = string.Join(" ", args);
                    fpm(arguments, workingDirectory: workingDirectory);
                }

                gzip($"-f {packageName}.tar", workingDirectory: workingDirectory);


                var suffix = RuntimeInformation.ProcessArchitecture == Architecture.X64
                    ? string.Empty
                    : $".{RuntimeInformation.ProcessArchitecture.ToString().ToLower()}";

                var versionedName = IsAlpine
                    ? $"{packageName}-{Version}-musl{suffix}.tar.gz"
                    : $"{packageName}-{Version}{suffix}.tar.gz";

                RenameFile(
                    workingDirectory / $"{packageName}.tar.gz",
                    workingDirectory / versionedName);
            }
        });


    Target CompileInstrumentationVerificationLibrary => _ => _
        .Unlisted()
        .After(Restore, CompileManagedSrc)
        .Executes(() =>
        {
            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatformAnyCPU()
                .SetProperty("BuildProjectReferences", true)
                .SetTargets("BuildInstrumentationVerificationLibrary"));
        });

    Target CompileManagedTestHelpers => _ => _
        .Unlisted()
        .DependsOn(CompileInstrumentationVerificationLibrary)
        .After(Restore)
        .After(CompileManagedSrc)
        .Executes(() =>
        {
            // Always AnyCPU
            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatformAnyCPU()
                .DisableRestore()
                .SetProperty("BuildProjectReferences", false)
                .SetTargets("BuildCsharpTestHelpers"));
        });

    Target CompileManagedUnitTests => _ => _
        .Unlisted()
        .After(Restore)
        .After(CompileManagedSrc)
        .After(BuildRunnerTool)
        .DependsOn(CopyLibDdwafForAppSecUnitTests)
        .DependsOn(CompileManagedTestHelpers)
        .Executes(() =>
        {
            // Always AnyCPU
            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatformAnyCPU()
                .DisableRestore()
                .SetProperty("BuildProjectReferences", false)
                .SetTargets("BuildCsharpUnitTests"));
        });

    Target RunManagedUnitTests => _ => _
        .Unlisted()
        .After(CompileManagedUnitTests)
        .Executes(() =>
        {
            EnsureExistingDirectory(TestLogsDirectory);

            var testProjects = TracerDirectory.GlobFiles("test/**/*.Tests.csproj")
                .Select(x => Solution.GetProject(x))
                .ToList();

            testProjects.ForEach(EnsureResultsDirectory);
            var filter = string.IsNullOrEmpty(Filter) && IsArm64 ? "(Category!=ArmUnsupported)" : Filter;
            try
            {
                DotNetTest(x => x
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(filter)
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatformAnyCPU()
                    .SetDDEnvironmentVariables("dd-tracer-dotnet")
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .When(!string.IsNullOrEmpty(Filter), c => c.SetFilter(Filter))
                    .CombineWith(testProjects, (x, project) => x
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .SetProjectFile(project)));
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target RunNativeTestsWindows => _ => _
        .Unlisted()
        .After(CompileNativeSrcWindows)
        .After(CompileNativeTestsWindows)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            var workingDirectory = TestsDirectory / "Datadog.Tracer.Native.Tests" / "bin" / BuildConfiguration.ToString() / TargetPlatform.ToString();
            var exePath = workingDirectory / "Datadog.Tracer.Native.Tests.exe";
            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe("--gtest_output=xml", workingDirectory: workingDirectory);
        });

    Target RunNativeTestsLinux => _ => _
        .Unlisted()
        .After(CompileNativeSrcLinux)
        .After(CompileNativeTestsLinux)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            Logger.Error("We don't currently run unit tests on Linux");
        });

    Target RunNativeTests => _ => _
        .Unlisted()
        .DependsOn(RunNativeTestsWindows)
        .DependsOn(RunNativeTestsLinux)
        .DependsOn(RunProfilerNativeUnitTestsWindows)
        .DependsOn(RunProfilerNativeUnitTestsLinux);

    Target CompileDependencyLibs => _ => _
        .Unlisted()
        .After(Restore)
        .After(CompileManagedSrc)
        .Executes(() =>
        {
            // Always AnyCPU
            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatformAnyCPU()
                .DisableRestore()
                .EnableNoDependencies()
                .SetTargets("BuildDependencyLibs")
            );
        });

    Target CompileRegressionDependencyLibs => _ => _
        .Unlisted()
        .After(Restore)
        .After(CompileManagedSrc)
        .Executes(() =>
        {
            // We run linux integration tests in AnyCPU, but Windows on the specific architecture
            var platform = !IsWin ? MSBuildTargetPlatform.MSIL : TargetPlatform;

            DotNetMSBuild(x => x
                .SetTargetPath(MsBuildProject)
                .SetTargetPlatformAnyCPU()
                .DisableRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatform(platform)
                .SetTargets("BuildRegressionDependencyLibs")
            );
        });

    Target CompileRegressionSamples => _ => _
        .Unlisted()
        .After(Restore)
        .After(CreatePlatformlessSymlinks)
        .After(CompileRegressionDependencyLibs)
        .Requires(() => Framework)
        .Executes(() =>
        {
            var regressionsDirectory = Solution.GetProject(Projects.DataDogThreadTest)
                .Directory.Parent;

            var regressionLibs = GlobFiles(regressionsDirectory / "**" / "*.csproj")
                 .Where(path =>
                    (path, Solution.GetProject(path).TryGetTargetFrameworks()) switch
                    {
                        _ when path.Contains("ExpenseItDemo") => false,
                        _ when path.Contains("StackExchange.Redis.AssemblyConflict.LegacyProject") => false,
                        _ when path.Contains("MismatchedTracerVersions") => false,
                        _ when path.Contains("dependency-libs") => false,
                        _ when !string.IsNullOrWhiteSpace(SampleName) => path.Contains(SampleName),
                        (_, var targets) when targets is not null => targets.Contains(Framework),
                        _ => true,
                    }
                  );

            // Allow restore here, otherwise things go wonky with runtime identifiers
            // in some target frameworks. No, I don't know why
            DotNetBuild(x => x
                // .EnableNoRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatform(TargetPlatform)
                .SetFramework(Framework)
                .SetNoWarnDotNetCore3()
                .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                    o.SetPackageDirectory(NugetPackageDirectory))
                .CombineWith(regressionLibs, (x, project) => x
                    .SetProjectFile(project)));
        });

    Target CompileFrameworkReproductions => _ => _
        .Unlisted()
        .Description("Builds .NET Framework projects (non SDK-based projects)")
        .After(CompileRegressionDependencyLibs)
        .After(CompileDependencyLibs)
        .After(CreatePlatformlessSymlinks)
        .Requires(() => IsWin)
        .Executes(() =>
        {
            // We have to use the full MSBuild here, as dotnet msbuild doesn't copy the EDMX assets for embedding correctly
            // seems similar to https://github.com/dotnet/sdk/issues/8360
            MSBuild(s => s
                .SetTargetPath(MsBuildProject)
                .SetMSBuildPath()
                .DisableRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatform(TargetPlatform)
                .SetTargets("BuildFrameworkReproductions")
                .SetMaxCpuCount(null));
        });

    Target CompileIntegrationTests => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileRegressionSamples)
        .After(CompileFrameworkReproductions)
        .After(PublishIisSamples)
        .After(BuildRunnerTool)
        .Requires(() => Framework)
        .Requires(() => MonitoringHomeDirectory != null)
        .Executes(() =>
        {
            DotNetMSBuild(s => s
                .SetTargetPath(MsBuildProject)
                .SetProperty("TargetFramework", Framework.ToString())
                .DisableRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatform(TargetPlatform)
                .SetTargets("BuildCsharpIntegrationTests")
                .SetMaxCpuCount(null));
        });

    Target CompileSamplesWindows => _ => _
        .Unlisted()
        .After(CompileDependencyLibs)
        .After(CreatePlatformlessSymlinks)
        .After(CompileFrameworkReproductions)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Framework)
        .Executes(() =>
        {
            // This does some "unnecessary" rebuilding and restoring
            var includeIntegration = TracerDirectory.GlobFiles("test/test-applications/integrations/**/*.csproj");
            // Don't build aspnet full framework sample in this step
            var includeSecurity = TracerDirectory.GlobFiles("test/test-applications/security/*/*.csproj");
            var includeDebugger = TracerDirectory.GlobFiles("test/test-applications/debugger/*/*.csproj");

            var exclude = TracerDirectory.GlobFiles("test/test-applications/integrations/dependency-libs/**/*.csproj");

            var projects = includeIntegration
                .Concat(includeSecurity)
                .Concat(includeDebugger)
                .Select(x => Solution.GetProject(x))
                .Where(project =>
                (project, project.TryGetTargetFrameworks(), project.RequiresDockerDependency()) switch
                {
                    _ when exclude.Contains(project.Path) => false,
                    _ when !string.IsNullOrWhiteSpace(SampleName) => project.Path.ToString().Contains(SampleName),
                    (_, _, true) => false, // can't use docker on Windows
                    var (_, targets, _) when targets is not null => targets.Contains(Framework),
                    _ => true,
                }
            );

            // /nowarn:NU1701 - Package 'x' was restored using '.NETFramework,Version=v4.6.1' instead of the project target framework '.NETCoreApp,Version=v2.1'.
            DotNetBuild(config => config
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatform(TargetPlatform)
                .EnableNoDependencies()
                .SetProperty("BuildInParallel", "true")
                .SetProcessArgumentConfigurator(arg => arg.Add("/nowarn:NU1701"))
                .CombineWith(projects, (s, project) => s
                    // we have to build this one for all frameworks (because of reasons)
                    .When(!project.Name.Contains("MultiDomainHost"), x => x.SetFramework(Framework))
                    .SetProjectFile(project)));
        });

    Target PublishIisSamples => _ => _
        .Unlisted()
        .After(CompileManagedTestHelpers)
        .After(CompileRegressionSamples)
        .After(CompileFrameworkReproductions)
        .Executes(() =>
        {
            var aspnetFolder = TestsDirectory / "test-applications" / "aspnet";
            var securityAspnetFolder = TestsDirectory / "test-applications" / "security" / "aspnet";

            var aspnetProjects = aspnetFolder.GlobFiles("**/*.csproj");
            var securityAspnetProjects = securityAspnetFolder.GlobFiles("**/*.csproj");

            var publishProfile = aspnetFolder / "PublishProfiles" / "FolderProfile.pubxml";

            MSBuild(x => x
                .SetMSBuildPath()
                // .DisableRestore()
                .EnableNoDependencies()
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatform(TargetPlatform)
                .SetProperty("DeployOnBuild", true)
                .SetProperty("PublishProfile", publishProfile)
                .SetMaxCpuCount(null)
                .CombineWith(aspnetProjects.Concat(securityAspnetProjects), (c, project) => c
                    .SetTargetPath(project))
            );
        });

    Target RunWindowsIntegrationTests => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .After(CompileIntegrationTests)
        .After(CompileSamplesWindows)
        .After(CompileFrameworkReproductions)
        .After(BuildWindowsIntegrationTests)
        .Requires(() => IsWin)
        .Requires(() => Framework)
        .Executes(() =>
        {
            EnsureExistingDirectory(TestLogsDirectory);
            ParallelIntegrationTests.ForEach(EnsureResultsDirectory);
            ClrProfilerIntegrationTests.ForEach(EnsureResultsDirectory);

            try
            {
                DotNetTest(config => config
                    .SetDotnetPath(TargetPlatform)
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatform(TargetPlatform)
                    .SetFramework(Framework)
                    //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetProcessEnvironmentVariable("TracerHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(!string.IsNullOrEmpty(Filter), c => c.SetFilter(Filter))
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ParallelIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .SetProjectFile(project)), degreeOfParallelism: 4);


                // TODO: I think we should change this filter to run on Windows by default
                // (RunOnWindows!=False|Category=Smoke)&LoadFromGAC!=True&IIS!=True
                DotNetTest(config => config
                    .SetDotnetPath(TargetPlatform)
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatform(TargetPlatform)
                    .SetFramework(Framework)
                    //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(Filter ?? "RunOnWindows=True&LoadFromGAC!=True&IIS!=True")
                    .SetProcessEnvironmentVariable("TracerHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ClrProfilerIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .SetProjectFile(project)));
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target RunWindowsRegressionTests => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .After(CompileIntegrationTests)
        .After(CompileRegressionSamples)
        .After(CompileFrameworkReproductions)
        .After(BuildNativeLoader)
        .Requires(() => IsWin)
        .Requires(() => Framework)
        .Executes(() =>
        {
            EnsureExistingDirectory(TestLogsDirectory);
            ClrProfilerIntegrationTests.ForEach(EnsureResultsDirectory);

            try
            {
                DotNetTest(config => config
                    .SetDotnetPath(TargetPlatform)
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatform(TargetPlatform)
                    .SetFramework(Framework)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(Filter ?? "Category=Smoke&LoadFromGAC!=True")
                    .SetProcessEnvironmentVariable("TracerHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ClrProfilerIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .SetProjectFile(project)));
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });


    Target RunWindowsTracerIisIntegrationTests => _ => _
        .After(BuildTracerHome)
        .After(CompileIntegrationTests)
        .After(CompileFrameworkReproductions)
        .After(PublishIisSamples)
        .Requires(() => Framework)
        .Executes(() => RunWindowsIisIntegrationTests(
                      Solution.GetProject(Projects.ClrProfilerIntegrationTests)));

    Target RunWindowsSecurityIisIntegrationTests => _ => _
        .After(BuildTracerHome)
        .After(CompileIntegrationTests)
        .After(CompileFrameworkReproductions)
        .After(PublishIisSamples)
        .Requires(() => Framework)
        .Executes(() => RunWindowsIisIntegrationTests(
                      Solution.GetProject(Projects.AppSecIntegrationTests)));

    void RunWindowsIisIntegrationTests(Project project)
    {
        EnsureResultsDirectory(project);
        try
        {
            // Different filter from RunWindowsIntegrationTests
            DotNetTest(config => config
                                .SetDotnetPath(TargetPlatform)
                                .SetConfiguration(BuildConfiguration)
                                .SetTargetPlatform(TargetPlatform)
                                .SetFramework(Framework)
                                .EnableNoRestore()
                                .EnableNoBuild()
                                .SetFilter(Filter ?? "(RunOnWindows=True)&LoadFromGAC=True")
                                .SetProcessEnvironmentVariable("TracerHomeDirectory", MonitoringHomeDirectory)
                                .SetLogsDirectory(TestLogsDirectory)
                                .When(CodeCoverage, ConfigureCodeCoverage)
                                .EnableTrxLogOutput(GetResultsDirectory(project))
                                .SetProjectFile(project));
        }
        finally
        {
            CopyDumpsToBuildData();
        }
    }

    Target RunWindowsMsiIntegrationTests => _ => _
        .After(BuildTracerHome)
        .After(CompileIntegrationTests)
        .After(CompileFrameworkReproductions)
        .After(PublishIisSamples)
        .Requires(() => Framework)
        .Executes(() =>
        {
            var project = Solution.GetProject(Projects.ClrProfilerIntegrationTests);
            var resultsDirectory = GetResultsDirectory(project);
            EnsureCleanDirectory(resultsDirectory);
            try
            {
                // Different filter from RunWindowsIntegrationTests
                DotNetTest(config => config
                    .SetDotnetPath(TargetPlatform)
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatform(TargetPlatform)
                    .SetFramework(Framework)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(Filter ?? "(RunOnWindows=True)&MSI=True")
                    .SetProcessEnvironmentVariable("TracerHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .EnableTrxLogOutput(resultsDirectory)
                    .SetProjectFile(project));
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target CompileSamplesLinux => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileRegressionDependencyLibs)
        .After(CompileDependencyLibs)
        .After(CompileManagedTestHelpers)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Framework)
        .Executes(() =>
        {
            MakeGrpcToolsExecutable();

            // There's nothing specifically linux-y here, it's just that we only build a subset of projects
            // for testing on linux.
            var sampleProjects = TracerDirectory.GlobFiles("test/test-applications/integrations/*/*.csproj");
            var securitySampleProjects = TracerDirectory.GlobFiles("test/test-applications/security/*/*.csproj");
            var regressionProjects = TracerDirectory.GlobFiles("test/test-applications/regression/*/*.csproj");
            var instrumentationProjects = TracerDirectory.GlobFiles("test/test-applications/instrumentation/*/*.csproj");
            var debuggerProjects = TracerDirectory.GlobFiles("test/test-applications/debugger/*/*.csproj");

            // These samples are currently skipped.
            var projectsToSkip = new[]
            {
                "Samples.Msmq",  // Doesn't run on Linux
                "Samples.Owin.WebApi2", // Doesn't run on Linux
                "Samples.RateLimiter", // I think we _should_ run this one (assuming it has tests)
                "Samples.SqlServer.NetFramework20",
                "Samples.TracingWithoutLimits", // I think we _should_ run this one (assuming it has tests)
                "Samples.Wcf",
                "Samples.WebRequest.NetFramework20",
                "DogStatsD.RaceCondition",
                "Sandbox.ManualTracing",
                "StackExchange.Redis.AssemblyConflict.LegacyProject",
                "Samples.OracleMDA", // We don't test these yet
                "Samples.OracleMDA.Core", // We don't test these yet
                "MismatchedTracerVersions",
                "IBM.Data.DB2.DBCommand",
                "Sandbox.AutomaticInstrumentation", // Doesn't run on Linux
            };

            // These sample projects are built using RestoreAndBuildSamplesForPackageVersions
            // so no point building them now
            var multiPackageProjects = new List<string>();
            if (TestAllPackageVersions)
            {
                var samplesFile = BuildDirectory / "PackageVersionsGeneratorDefinitions.json";
                using var fs = File.OpenRead(samplesFile);
                var json = JsonDocument.Parse(fs);
                multiPackageProjects = json.RootElement
                                           .EnumerateArray()
                                           .Select(e => e.GetProperty("SampleProjectName").GetString())
                                           .Distinct()
                                           .Where(name => name switch
                                            {
                                                "Samples.MySql" => false, // the "non package version" is _ALSO_ tested separately
                                                _ => true
                                            })
                                           .ToList();
            }

            var projectsToBuild = sampleProjects
                .Concat(securitySampleProjects)
                .Concat(regressionProjects)
                .Concat(instrumentationProjects)
                .Concat(debuggerProjects)
                .Select(path => (path, project: Solution.GetProject(path)))
                .Where(x => (IncludeTestsRequiringDocker, x.project) switch
                {
                    // filter out or to integration tests that have docker dependencies
                    (null, _) => true,
                    (_, null) => true,
                    (_, { } p) when p.Name.Contains("Samples.Probes") => true, // always have to build this one
                    (_, { } p) when p.Name.Contains("Samples.AspNetCoreRazorPages") => true, // always have to build this one
                    (_, { } p) when !string.IsNullOrWhiteSpace(SampleName) && p.Name.Contains(SampleName) => true,
                    (var required, { } p) => p.RequiresDockerDependency() == required,
                })
                .Where(x =>
                {
                    return x.project?.Name switch
                    {
                        "LogsInjection.Log4Net.VersionConflict.2x" => Framework != TargetFramework.NETCOREAPP2_1,
                        "LogsInjection.NLog.VersionConflict.2x" => Framework != TargetFramework.NETCOREAPP2_1,
                        "LogsInjection.NLog10.VersionConflict.2x" => Framework == TargetFramework.NET461,
                        "LogsInjection.NLog20.VersionConflict.2x" => Framework == TargetFramework.NET461,
                        "LogsInjection.Serilog.VersionConflict.2x" => Framework != TargetFramework.NETCOREAPP2_1,
                        "LogsInjection.Serilog14.VersionConflict.2x" => Framework == TargetFramework.NET461,
                        "Samples.AspNetCoreMvc21" => Framework == TargetFramework.NETCOREAPP2_1,
                        "Samples.AspNetCoreMvc30" => Framework == TargetFramework.NETCOREAPP3_0,
                        "Samples.AspNetCoreMvc31" => Framework == TargetFramework.NETCOREAPP3_1,
                        "Samples.AspNetCoreMinimalApis" => Framework == TargetFramework.NET6_0,
                        "Samples.Security.AspNetCore2" => Framework == TargetFramework.NETCOREAPP2_1,
                        "Samples.Security.AspNetCore5" => Framework == TargetFramework.NET6_0 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NETCOREAPP3_0,
                        "Samples.Security.AspNetCoreBare" => Framework == TargetFramework.NET6_0 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NETCOREAPP3_0,
                        "Samples.GraphQL4" => Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NET6_0,
                        "Samples.AWS.Lambda" => Framework == TargetFramework.NETCOREAPP3_1,
                        var name when projectsToSkip.Contains(name) => false,
                        var name when multiPackageProjects.Contains(name) => false,
                        "Samples.AspNetCoreRazorPages" => true,
                        _ when !string.IsNullOrWhiteSpace(SampleName) => x.project?.Name?.Contains(SampleName) ?? false,
                        _ => true,
                    };
                })
                .Select(x => x.path);

            // do the build and publish separately to avoid dependency issues

            // Always AnyCPU
            DotNetBuild(x => x
                    // .EnableNoRestore()
                    .EnableNoDependencies()
                    .SetConfiguration(BuildConfiguration)
                    .SetFramework(Framework)
                    // .SetTargetPlatform(Platform)
                    .SetNoWarnDotNetCore3()
                    .When(TestAllPackageVersions, o => o.SetProperty("TestAllPackageVersions", "true"))
                    .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
                    .CombineWith(projectsToBuild, (c, project) => c
                        .SetProjectFile(project)));

            // Always AnyCPU
            DotNetPublish(x => x
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .EnableNoDependencies()
                    .SetConfiguration(BuildConfiguration)
                    .SetFramework(Framework)
                    // .SetTargetPlatform(Platform)
                    .SetNoWarnDotNetCore3()
                    .When(TestAllPackageVersions, o => o.SetProperty("TestAllPackageVersions", "true"))
                    .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
                    .CombineWith(projectsToBuild, (c, project) => c
                        .SetProject(project)));

        });

    Target CompileMultiApiPackageVersionSamples => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileRegressionDependencyLibs)
        .After(CompileDependencyLibs)
        .After(CompileManagedTestHelpers)
        .After(CompileSamplesLinux)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Framework)
        .Executes(() =>
        {
            // Build and restore for all versions
            // Annoyingly this rebuilds everything again and again.
            var targets = new[] { "RestoreSamplesForPackageVersionsOnly", "RestoreAndBuildSamplesForPackageVersionsOnly" };

            // /nowarn:NU1701 - Package 'x' was restored using '.NETFramework,Version=v4.6.1' instead of the project target framework '.NETCoreApp,Version=v2.1'.
            // /nowarn:NETSDK1138 - Package 'x' was restored using '.NETFramework,Version=v4.6.1' instead of the project target framework '.NETCoreApp,Version=v2.1'.
            foreach (var target in targets)
            {
                // TODO: When IncludeTestsRequiringDocker is set, only build required samples
                DotNetMSBuild(x => x
                    .SetTargetPath(MsBuildProject)
                    .SetTargets(target)
                    .SetConfiguration(BuildConfiguration)
                    .EnableNoDependencies()
                    .SetProperty("TargetFramework", Framework.ToString())
                    .SetProperty("BuildInParallel", "true")
                    .SetProperty("CheckEolTargetFramework", "false")
                    .When(IsArm64, o => o.SetProperty("IsArm64", "true"))
                    .When(IsAlpine, o => o.SetProperty("IsAlpine", "true"))
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetProperty("RestorePackagesPath", NugetPackageDirectory))
                    .SetProcessArgumentConfigurator(arg => arg.Add("/nowarn:NU1701"))
                    .When(TestAllPackageVersions, o => o.SetProperty("TestAllPackageVersions", "true"))
                    .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                );

                MakeGrpcToolsExecutable(); // for use in the second target
            }
        });

    Target CompileLinuxIntegrationTests => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileRegressionDependencyLibs)
        .After(CompileDependencyLibs)
        .After(CompileManagedTestHelpers)
        .After(CompileSamplesLinux)
        .After(CompileMultiApiPackageVersionSamples)
        .After(BuildRunnerTool)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Framework)
        .Executes(() =>
        {
            // Build the actual integration test projects for Any CPU
            var integrationTestProjects = TracerDirectory.GlobFiles("test/*.IntegrationTests/*.csproj");
            DotNetBuild(x => x
                    // .EnableNoRestore()
                    .EnableNoDependencies()
                    .SetConfiguration(BuildConfiguration)
                    .SetFramework(Framework)
                    // .SetTargetPlatform(Platform)
                    .SetNoWarnDotNetCore3()
                    .When(TestAllPackageVersions, o => o.SetProperty("TestAllPackageVersions", "true"))
                    .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                        o.SetPackageDirectory(NugetPackageDirectory))
                    .CombineWith(integrationTestProjects, (c, project) => c
                        .SetProjectFile(project)));

            IntegrationTestLinuxProfilerDirFudge(Projects.ClrProfilerIntegrationTests);
            IntegrationTestLinuxProfilerDirFudge(Projects.AppSecIntegrationTests);
        });

    Target RunLinuxIntegrationTests => _ => _
        .After(CompileLinuxIntegrationTests)
        .Description("Runs the linux integration tests")
        .Requires(() => Framework)
        .Requires(() => !IsWin)
        .Executes(() =>
        {
            EnsureExistingDirectory(TestLogsDirectory);
            ParallelIntegrationTests.ForEach(EnsureResultsDirectory);
            ClrProfilerIntegrationTests.ForEach(EnsureResultsDirectory);

            var dockerFilter = IncludeTestsRequiringDocker switch
            {
                true => "&(RequiresDockerDependency=true)",
                false => "&(RequiresDockerDependency!=true)",
                null => string.Empty,
            };

            var filter = (string.IsNullOrEmpty(Filter), IsArm64) switch
            {
                (true, false) => $"(Category!=LinuxUnsupported){dockerFilter}",
                (true, true) => $"(Category!=LinuxUnsupported){dockerFilter}&(Category!=ArmUnsupported)",
                _ => Filter
            };

            try
            {
                // Run these ones in parallel
                // Always AnyCPU
                DotNetTest(config => config
                        .SetConfiguration(BuildConfiguration)
                        // .SetTargetPlatform(Platform)
                        .EnableNoRestore()
                        .EnableNoBuild()
                        .SetFramework(Framework)
                        //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                        .SetFilter(filter)
                        .SetProcessEnvironmentVariable("TracerHomeDirectory", MonitoringHomeDirectory)
                        .SetLogsDirectory(TestLogsDirectory)
                        .When(TestAllPackageVersions, o => o.SetProcessEnvironmentVariable("TestAllPackageVersions", "true"))
                        .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                        .When(IncludeTestsRequiringDocker is not null, o => o.SetProperty("IncludeTestsRequiringDocker", IncludeTestsRequiringDocker.Value ? "true" : "false"))
                        .When(CodeCoverage, ConfigureCodeCoverage)
                        .CombineWith(ParallelIntegrationTests, (s, project) => s
                            .EnableTrxLogOutput(GetResultsDirectory(project))
                            .SetProjectFile(project)),
                    degreeOfParallelism: 2);

                // Run this one separately so we can tail output
                DotNetTest(config => config
                    .SetConfiguration(BuildConfiguration)
                    // .SetTargetPlatform(Platform)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFramework(Framework)
                    //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                    .SetFilter(filter)
                    .SetProcessEnvironmentVariable("TracerHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(TestAllPackageVersions, o => o.SetProcessEnvironmentVariable("TestAllPackageVersions", "true"))
                    .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                    .When(IncludeTestsRequiringDocker is not null, o => o.SetProperty("IncludeTestsRequiringDocker", IncludeTestsRequiringDocker.Value ? "true" : "false"))
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ClrProfilerIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .SetProjectFile(project))
                );
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target InstallDdTraceTool => _ => _
         .Description("Installs the dd-trace tool")
         .OnlyWhenDynamic(() => (ToolSource != null))
         .Executes(() =>
         {
             try
             {
                 DotNetToolUninstall(s => s
                     .SetToolInstallationPath(ToolInstallDirectory)
                     .SetPackageName("dd-trace")
                     .DisableProcessLogOutput());
             }
             catch
             {
                 // This step is expected to fail if the tool is not already installed
                 Logger.Info("Could not uninstall the dd-trace tool. It's probably not installed.");
             }

             DotNetToolInstall(s => s
                .SetToolInstallationPath(ToolInstallDirectory)
                .SetSources(ToolSourceDirectory)
                .SetProcessArgumentConfigurator(args => args.Add("--no-cache"))
                .SetPackageName("dd-trace"));
         });

    Target BuildToolArtifactTests => _ => _
         .Description("Builds the tool artifacts tests")
         .After(CompileManagedTestHelpers)
         .After(InstallDdTraceTool)
         .Executes(() =>
          {
              DotNetBuild(x => x
                  .SetProjectFile(Solution.GetProject(Projects.ToolArtifactsTests))
                  .EnableNoDependencies()
                  .EnableNoRestore()
                  .SetConfiguration(BuildConfiguration)
                  .SetNoWarnDotNetCore3());
          });

    Target RunToolArtifactTests => _ => _
       .Description("Runs the tool artifacts tests")
       .After(BuildToolArtifactTests)
       .Executes(() =>
        {
            var project = Solution.GetProject(Projects.ToolArtifactsTests);

            DotNetTest(config => config
                .SetProjectFile(project)
                .SetConfiguration(BuildConfiguration)
                .EnableNoRestore()
                .EnableNoBuild()
                .SetProcessEnvironmentVariable("TracerHomeDirectory", MonitoringHomeDirectory)
                .SetProcessEnvironmentVariable("ToolInstallDirectory", ToolInstallDirectory)
                .SetLogsDirectory(TestLogsDirectory)
                .EnableTrxLogOutput(GetResultsDirectory(project)));
        });


    Target CheckBuildLogsForErrors => _ => _
       .Unlisted()
       .Description("Reads the logs from build_data and checks for error lines")
       .Executes(() =>
       {
           // we expect to see _some_ errors, so explcitly ignore them
           var knownPatterns = new List<Regex>
           {
               new(@".*Unable to resolve method MongoDB\..*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsIntegration\.OnAsyncMethodEnd.*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsIntegration\.OnMethodBegin.*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsIntegration\.OnMethodEnd.*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsVoidIntegration\.OnMethodBegin.*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsVoidIntegration\.OnMethodEnd.*", RegexOptions.Compiled),
               new(@".*System.Threading.ThreadAbortException: Thread was being aborted\.", RegexOptions.Compiled),
           };

           var logDirectory = BuildDataDirectory / "logs";
           if (DirectoryExists(logDirectory))
           {
               // Should we care about warnings too?
               var managedErrors = logDirectory.GlobFiles("**/dotnet-tracer-managed-*")
                                               .SelectMany(ParseManagedLogFiles)
                                               .Where(x => x.Level >= LogLevel.Error)
                                               .Where(IsNewError)
                                               .ToList();

               var nativeTracerErrors = logDirectory.GlobFiles("**/dotnet-tracer-native-*")
                                               .SelectMany(ParseNativeTracerLogFiles)
                                               .Where(x => x.Level >= LogLevel.Error)
                                               .Where(IsNewError)
                                               .ToList();

               var nativeProfilerErrors = logDirectory.GlobFiles("**/DD-DotNet-Profiler-Native-*")
                                               .SelectMany(ParseNativeProfilerLogFiles)
                                               .Where(x => x.Level >= LogLevel.Error)
                                               .Where(IsNewError)
                                               .ToList();

               if (managedErrors.Count == 0 && nativeTracerErrors.Count == 0 && nativeProfilerErrors.Count == 0)
               {
                   Logger.Info("No errors found in managed or native logs");
                   return;
               }

               Logger.Warn("Found the following errors in log files:");
               var allErrors = managedErrors
                              .Concat(nativeTracerErrors)
                              .Concat(nativeProfilerErrors)
                              .GroupBy(x => x.FileName);

               foreach (var erroredFile in allErrors)
               {
                   Logger.Error($"Found errors in log file '{erroredFile.Key}':");
                   foreach (var error in erroredFile)
                   {
                       Logger.Error($"{error.Timestamp:hh:mm:ss} [{error.Level}] {error.Message}");
                   }
               }

               ExitCode = 1;
           }

           bool IsNewError(ParsedLogLine logLine)
           {
               foreach (var pattern in knownPatterns)
               {
                   if (pattern.IsMatch(logLine.Message))
                   {
                       return false;
                   }
               }

               return true;
           }

           static List<ParsedLogLine> ParseManagedLogFiles(AbsolutePath logFile)
           {
               var regex = new Regex(@"^(\d\d\d\d\-\d\d\-\d\d\W\d\d\:\d\d\:\d\d\.\d\d\d\W\+\d\d\:\d\d)\W\[(.*?)\]\W(.*)", RegexOptions.Compiled);
               var allLines = File.ReadAllLines(logFile);
               var allLogs = new List<ParsedLogLine>(allLines.Length);
               ParsedLogLine currentLine = null;

               foreach (var line in allLines)
               {
                   if (string.IsNullOrWhiteSpace(line))
                   {
                       continue;
                   }
                   var match = regex.Match(line);

                   if (match.Success)
                   {
                       if (currentLine is not null)
                       {
                           allLogs.Add(currentLine);
                       }

                       try
                       {
                           // start of a new log line
                           var timestamp = DateTimeOffset.Parse(match.Groups[1].Value);
                           var level = ParseManagedLogLevel(match.Groups[2].Value);
                           var message = match.Groups[3].Value;
                           currentLine = new ParsedLogLine(timestamp, level, message, logFile);
                       }
                       catch (Exception ex)
                       {
                           Logger.Info($"Error parsing line: '{line}. {ex}");
                       }
                   }
                   else
                   {
                       if (currentLine is null)
                       {
                           Logger.Warn("Incomplete log line: " + line);
                       }
                       else
                       {
                           currentLine = currentLine with { Message = $"{currentLine.Message}{Environment.NewLine}{line}" };
                       }
                   }
               }

               return allLogs;
           }

           static List<ParsedLogLine> ParseNativeTracerLogFiles(AbsolutePath logFile)
           {
               var regex = new Regex(@"^(\d\d\/\d\d\/\d\d\W\d\d\:\d\d\:\d\d\.\d\d\d\W\w\w)\W\[.*?\]\W\[(.*?)\](.*)", RegexOptions.Compiled);
               return ParseNativeLogs(regex, "MM/dd/yy hh:mm:ss.fff tt", logFile);
           }

           static List<ParsedLogLine> ParseNativeProfilerLogFiles(AbsolutePath logFile)
           {
               var regex = new Regex(@"^\[(\d\d\d\d-\d\d-\d\d\W\d\d\:\d\d\:\d\d\.\d\d\d)\W\|\W([^ ]+)\W[^\]]+\W(.*)", RegexOptions.Compiled);
               return ParseNativeLogs(regex, "yyyy-MM-dd H:mm:ss.fff", logFile);
           }

           static List<ParsedLogLine> ParseNativeLogs(Regex regex, string dateFormat, AbsolutePath logFile)
           {
               var allLines = File.ReadAllLines(logFile);
               var allLogs = new List<ParsedLogLine>(allLines.Length);

               foreach (var line in allLines)
               {
                   if (string.IsNullOrWhiteSpace(line))
                   {
                       continue;
                   }
                   var match = regex.Match(line);
                   if (match.Success)
                   {
                       try
                       {
                           // native logs are on one line
                           var timestamp = DateTimeOffset.ParseExact(match.Groups[1].Value, dateFormat, null);
                           var level = ParseNativeLogLevel(match.Groups[2].Value);
                           var message = match.Groups[3].Value;
                           var currentLine = new ParsedLogLine(timestamp, level, message, logFile);
                           allLogs.Add(currentLine);
                       }
                       catch (Exception ex)
                       {
                           Logger.Info($"Error parsing line: '{line}. {ex}");
                       }
                   }
                   else
                   {
                       Logger.Warn("Incomplete log line: " + line);
                   }
               }

               return allLogs;
           }

           static LogLevel ParseManagedLogLevel(string value)
               => value switch
               {
                   "VRB" => LogLevel.Trace,
                   "DBG" => LogLevel.Trace,
                   "INF" => LogLevel.Normal,
                   "WRN" => LogLevel.Warning,
                   "ERR" => LogLevel.Error,
                   _ => LogLevel.Normal, // Concurrency issues can sometimes garble this so ignore it
               };

           static LogLevel ParseNativeLogLevel(string value)
               => value switch
               {
                   "trace" => LogLevel.Trace,
                   "debug" => LogLevel.Trace,
                   "info" => LogLevel.Normal,
                   "warning" => LogLevel.Warning,
                   "error" => LogLevel.Error,
                   _ => LogLevel.Normal, // Concurrency issues can sometimes garble this so ignore it
               };

           Logger.Info($"Skipping log parsing, directory '{logDirectory}' not found");
       });

    private void MakeGrpcToolsExecutable()
    {
        var packageDirectory = NugetPackageDirectory;
        if (string.IsNullOrEmpty(NugetPackageDirectory))
        {
            Logger.Info("NugetPackageDirectory not set, querying for global-package location");
            var packageLocation = "global-packages";
            var output = DotNet($"nuget locals {packageLocation} --list");

            var expected = $"{packageLocation}: ";
            var location = output
                              .Where(x => x.Type == OutputType.Std)
                              .Select(x => x.Text)
                              .FirstOrDefault(x => x.StartsWith(expected))
                             ?.Substring(expected.Length);

            if (string.IsNullOrEmpty(location))
            {
                Logger.Info("Couldn't determine global-package location, skipping chmod +x on grpc.tools");
                return;
            }

            packageDirectory = (AbsolutePath)(location);
        }

        Logger.Info($"Using '{packageDirectory}' for NuGet package location");

        // GRPC runs a tool for codegen, which apparently isn't automatically marked as executable
        var grpcTools = GlobFiles(packageDirectory / "grpc.tools", "**/tools/linux_*/*");
        foreach (var toolPath in grpcTools)
        {
            Chmod.Value.Invoke(" +x " + toolPath);
        }
    }

    private AbsolutePath GetResultsDirectory(Project proj) => BuildDataDirectory / "results" / proj.Name;

    private void EnsureResultsDirectory(Project proj) => EnsureCleanDirectory(GetResultsDirectory(proj));

    private (string, string) GetUnixArchitectureAndExtension() => IsOsx ? ("osx-x64", "dylib") : ($"linux-{LinuxArchitectureIdentifier}", "so");
    // the integration tests need their own copy of the profiler, this achieved through build.props on Windows, but doesn't seem to work under Linux
    private void IntegrationTestLinuxProfilerDirFudge(string project)
    {
        // Not sure if/why this is necessary, and we can't just point to the correct output location
        var src = MonitoringHomeDirectory;
        var testProject = Solution.GetProject(project).Directory;
        var dest = testProject / "bin" / BuildConfiguration / Framework / "profiler-lib";
        CopyDirectoryRecursively(src, dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);

        // not sure exactly where this is supposed to go, may need to change the original build
        foreach (var linuxDir in MonitoringHomeDirectory.GlobDirectories("linux-*"))
        {
            CopyDirectoryRecursively(linuxDir, dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
        }
    }

    private void CopyDumpsToBuildData()
    {
        if (Directory.Exists(TempDirectory))
        {
            foreach (var dump in GlobFiles(TempDirectory, "coredump*"))
            {
                MoveFileToDirectory(dump, BuildDataDirectory / "dumps", FileExistsPolicy.Overwrite);
            }
        }

        foreach (var file in Directory.EnumerateFiles(TracerDirectory, "*.dmp", SearchOption.AllDirectories))
        {
            CopyFileToDirectory(file, BuildDataDirectory, FileExistsPolicy.OverwriteIfNewer);
        }
    }

    private DotNetTestSettings ConfigureCodeCoverage(DotNetTestSettings settings)
    {
        var strongNameKeyPath = Solution.Directory / "Datadog.Trace.snk";

        return settings.SetDataCollector("XPlat Code Coverage")
                .SetProcessArgumentConfigurator(
                     args =>
                         args.Add("--")
                             .Add("RunConfiguration.DisableAppDomain=true") // https://github.com/coverlet-coverage/coverlet/issues/347
                             .Add("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.SkipAutoProps=true")
                             .Add("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura")
                             .Add($"DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.StrongNameKey=\"{strongNameKeyPath}\"")
                             .Add("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude=\"[*]Datadog.Trace.Vendors.*,[Datadog.Trace]System.*,[Datadog.Trace]Mono.*\",")
                             .Add("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=\"[Datadog.Trace.ClrProfiler.*]*,[Datadog.Trace]*,[Datadog.Trace.AspNet]*\""));
    }

    protected override void OnTargetStart(string target)
    {
        if (PrintDriveSpace)
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                Logger.Info($"Drive space available on '{drive.Name}': {PrettyPrint(drive.AvailableFreeSpace)} / {PrettyPrint(drive.TotalSize)}");
            }
        }
        base.OnTargetStart(target);

        static string PrettyPrint(long bytes)
        {
            var power = Math.Min((int)Math.Log(bytes, 1000), 4);
            var normalised = bytes / Math.Pow(1000, power);
            return power switch
            {
                4 => $"{normalised:F}TB",
                3 => $"{normalised:F}GB",
                2 => $"{normalised:F}MB",
                1 => $"{normalised:F}KB",
                _ => $"{bytes}B",
            };
        }
    }

    private record ParsedLogLine(DateTimeOffset Timestamp, LogLevel Level, string Message, AbsolutePath FileName);
}
