using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    AbsolutePath ArtifactsDirectory => Artifacts ?? (OutputDirectory / "artifacts");
    AbsolutePath WindowsTracerHomeZip => ArtifactsDirectory / "windows-tracer-home.zip";
    AbsolutePath WindowsSymbolsZip => ArtifactsDirectory / "windows-native-symbols.zip";
    AbsolutePath OsxTracerHomeZip => ArtifactsDirectory / "macOS-tracer-home.zip";
    AbsolutePath BuildDataDirectory => TracerDirectory / "build_data";
    AbsolutePath TestLogsDirectory => BuildDataDirectory / "logs";
    AbsolutePath ToolSourceDirectory => ToolSource ?? (OutputDirectory / "runnerTool");
    AbsolutePath ToolInstallDirectory => ToolDestination ?? (ToolSourceDirectory / "install");

    AbsolutePath MonitoringHomeDirectory => MonitoringHome ?? (SharedDirectory / "bin" / "monitoring-home");

    [Solution("profiler/src/Demos/Datadog.Demos.sln")] readonly Solution ProfilerSamplesSolution;
    [Solution("Datadog.Profiler.sln")] readonly Solution ProfilerSolution;
    AbsolutePath ProfilerMsBuildProject => ProfilerDirectory / "src" / "ProfilerEngine" / "Datadog.Profiler.Native.Windows" / "Datadog.Profiler.Native.Windows.WithTests.proj";
    AbsolutePath ProfilerOutputDirectory => RootDirectory / "profiler" / "_build";
    AbsolutePath ProfilerBuildDataDirectory => ProfilerDirectory / "build_data";
    AbsolutePath ProfilerTestLogsDirectory => ProfilerBuildDataDirectory / "logs";

    AbsolutePath NativeBuildDirectory => RootDirectory / "obj";

    const string LibDdwafVersion = "1.9.0";

    const string OlderLibDdwafVersion = "1.4.0";

    AbsolutePath LibDdwafDirectory(string libDdwafVersion = null) => (NugetPackageDirectory ?? RootDirectory / "packages") / $"libddwaf.{libDdwafVersion ?? LibDdwafVersion}";

    AbsolutePath SourceDirectory => TracerDirectory / "src";
    AbsolutePath BuildDirectory => TracerDirectory / "build";
    AbsolutePath TestsDirectory => TracerDirectory / "test";
    AbsolutePath BundleHomeDirectory => Solution.GetProject(Projects.DatadogTraceBundle).Directory / "home";

    AbsolutePath SharedTestsDirectory => SharedDirectory / "test";

    AbsolutePath TempDirectory => (AbsolutePath)(IsWin ? Path.GetTempPath() : "/tmp/");

    readonly string[] WafWindowsArchitectureFolders = { "win-x86", "win-x64" };
    Project NativeTracerProject => Solution.GetProject(Projects.ClrProfilerNative);
    Project NativeLoaderProject => Solution.GetProject(Projects.NativeLoader);
    Project NativeLoaderTestsProject => Solution.GetProject(Projects.NativeLoaderNativeTests);

    [LazyPathExecutable(name: "cmake")] readonly Lazy<Tool> CMake;
    [LazyPathExecutable(name: "make")] readonly Lazy<Tool> Make;
    [LazyPathExecutable(name: "fpm")] readonly Lazy<Tool> Fpm;
    [LazyPathExecutable(name: "gzip")] readonly Lazy<Tool> GZip;
    [LazyPathExecutable(name: "cmd")] readonly Lazy<Tool> Cmd;
    [LazyPathExecutable(name: "chmod")] readonly Lazy<Tool> Chmod;
    [LazyPathExecutable(name: "objcopy")] readonly Lazy<Tool> ExtractDebugInfo;
    [LazyPathExecutable(name: "strip")] readonly Lazy<Tool> StripBinary;
    [LazyPathExecutable(name: "ln")] readonly Lazy<Tool> HardLinkUtil;
    [LazyPathExecutable(name: "cppcheck")] readonly Lazy<Tool> CppCheck;
    [LazyPathExecutable(name: "run-clang-tidy")] readonly Lazy<Tool> RunClangTidy;

    //OSX Tools
    readonly string[] OsxArchs = { "arm64", "x86_64" };
    [LazyPathExecutable(name: "otool")] readonly Lazy<Tool> OTool;
    [LazyPathExecutable(name: "lipo")] readonly Lazy<Tool> Lipo;

    IEnumerable<MSBuildTargetPlatform> ArchitecturesForPlatformForTracer
    {
        get
        {
            if (TargetPlatform == MSBuildTargetPlatform.x64)
            {
                if (ForceARM64BuildInWindows)
                {
                    return new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, ARM64ECTargetPlatform };
                }
                else
                {
                    return new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 };
                }
            }
            else if (TargetPlatform == ARM64TargetPlatform)
            {
                return new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, ARM64ECTargetPlatform };
            }
            else if (TargetPlatform == MSBuildTargetPlatform.x86)
            {
                return new[] { MSBuildTargetPlatform.x86 };
            }

            return new[] { TargetPlatform };
        }
    }

    IEnumerable<MSBuildTargetPlatform> ArchitecturesForPlatformForProfiler
    {
        get
        {
            if (TargetPlatform == MSBuildTargetPlatform.x64)
            {
                return new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 };
            }
            else if (TargetPlatform == MSBuildTargetPlatform.x86)
            {
                return new[] { MSBuildTargetPlatform.x86 };
            }

            return new[] { TargetPlatform };
        }
    }

    bool IsArm64 => RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    string UnixArchitectureIdentifier => IsArm64 ? "arm64" : TargetPlatform.ToString();

    IEnumerable<string> LinuxPackageTypes => IsAlpine ? new[] { "tar" } : new[] { "deb", "rpm", "tar" };

    IEnumerable<Project> ProjectsToPack => new[]
    {
        Solution.GetProject(Projects.DatadogTrace),
        Solution.GetProject(Projects.DatadogTraceOpenTracing),
        Solution.GetProject(Projects.DatadogTraceAnnotations),
        Solution.GetProject(Projects.DatadogTraceBenchmarkDotNet),
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

    TargetFramework[] TestingFrameworks =>
    IncludeAllTestFrameworks || HaveIntegrationsChanged
        ? new[] { TargetFramework.NET462, TargetFramework.NETCOREAPP2_1, TargetFramework.NETCOREAPP3_0, TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET6_0, TargetFramework.NET7_0, }
        : new[] { TargetFramework.NET462, TargetFramework.NETCOREAPP2_1, TargetFramework.NETCOREAPP3_1, TargetFramework.NET7_0, };

    bool HaveIntegrationsChanged =>
        GetGitChangedFiles(baseBranch: "origin/master")
           .Any(s => new []
            {
                "tracer/src/Datadog.Trace/Generated/net461/Datadog.Trace.SourceGenerators/Datadog.Trace.SourceGenerators.InstrumentationDefinitions.InstrumentationDefinitionsGenerator",
                "tracer/src/Datadog.Trace/Generated/netstandard2.0/Datadog.Trace.SourceGenerators/Datadog.Trace.SourceGenerators.InstrumentationDefinitions.InstrumentationDefinitionsGenerator",
                "tracer/src/Datadog.Trace/Generated/netcoreapp3.1/Datadog.Trace.SourceGenerators/Datadog.Trace.SourceGenerators.InstrumentationDefinitions.InstrumentationDefinitionsGenerator",
                "tracer/src/Datadog.Trace/Generated/net6.0/Datadog.Trace.SourceGenerators/Datadog.Trace.SourceGenerators.InstrumentationDefinitions.InstrumentationDefinitionsGenerator",
            }.Any(s.Contains));

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
            EnsureExistingDirectory(BuildDataDirectory);
            EnsureExistingDirectory(ProfilerBuildDataDirectory);
            EnsureExistingDirectory(SymbolsDirectory);
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
            var platforms = ArchitecturesForPlatformForTracer;

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

    Target CompileTracerNativeSrcLinux => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");
            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target {FileNames.NativeTracer}");
        });

    Target CompileNativeSrcMacOs => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .OnlyWhenStatic(() => IsOsx)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            var lstNativeBinaries = new List<string>();
            foreach (var arch in OsxArchs)
            {
                DeleteDirectory(NativeBuildDirectory);

                var envVariables = new Dictionary<string, string> { ["CMAKE_OSX_ARCHITECTURES"] = arch };

                // Build native
                CMake.Value(
                    arguments: $"-B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}",
                    environmentVariables: envVariables);
                CMake.Value(
                    arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target {FileNames.NativeTracer}",
                    environmentVariables: envVariables);

                var sourceFile = NativeTracerProject.Directory / "build" / "bin" / $"{NativeTracerProject.Name}.dylib";
                var destFile = NativeTracerProject.Directory / "build" / "bin" / $"{NativeTracerProject.Name}.{arch}.dylib";

                // Check section with the manager loader
                var output = OTool.Value(arguments: $"-s binary dll {sourceFile}", logOutput: false);
                var outputCount = output.Select(o => o.Type == OutputType.Std).Count();
                if (outputCount < 1000)
                {
                    throw new ApplicationException("Managed loader section doesn't have the enough size > 1000");
                }

                // Check the architecture of the build
                output = Lipo.Value(arguments: $"-archs {sourceFile}", logOutput: false);
                var strOutput = string.Join('\n', output.Where(o => o.Type == OutputType.Std).Select(o => o.Text));
                if (!strOutput.Contains(arch, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ApplicationException($"Invalid architecture, expected: '{arch}', actual: '{strOutput}'");
                }

                // Copy binary to the temporal destination
                CopyFile(sourceFile, destFile, FileExistsPolicy.Overwrite);

                // Add library to the list
                lstNativeBinaries.Add(destFile);
            }

            // Create universal shared library with all architectures in a single file
            var destination = NativeTracerProject.Directory / "build" / "bin" / $"{NativeTracerProject.Name}.dylib";
            DeleteFile(destination);
            Console.WriteLine($"Creating universal binary for {destination}");
            var strNativeBinaries = string.Join(' ', lstNativeBinaries);
            Lipo.Value(arguments: $"{strNativeBinaries} -create -output {destination}");
        });

    Target CompileNativeSrc => _ => _
        .Unlisted()
        .Description("Compiles the native loader")
        .DependsOn(CompileNativeSrcWindows)
        .DependsOn(CompileNativeSrcMacOs)
        .DependsOn(CompileTracerNativeSrcLinux);

    Target CppCheckNativeSrcUnix => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux || IsOsx)
        .Executes(() =>
        {
            var (arch, ext) = GetUnixArchitectureAndExtension();
            CppCheck.Value(arguments: $"--inconclusive --project={NativeTracerProject.Path} --output-file={BuildDataDirectory}/{NativeTracerProject.Name}-cppcheck-{arch}.xml --xml --enable=all --suppress=\"noExplicitConstructor\" --suppress=\"cstyleCast\" --suppress=\"duplicateBreak\" --suppress=\"unreadVariable\" --suppress=\"functionConst\" --suppress=\"funcArgNamesDifferent\" --suppress=\"variableScope\" --suppress=\"useStlAlgorithm\" --suppress=\"functionStatic\" --suppress=\"initializerList\" --suppress=\"redundantAssignment\" --suppress=\"redundantInitialization\" --suppress=\"shadowVariable\" --suppress=\"constParameter\" --suppress=\"unusedPrivateFunction\" --suppress=\"unusedFunction\" --suppress=\"missingInclude\" --suppress=\"unmatchedSuppression\" --suppress=\"knownConditionTrueFalse\"");
            CppCheck.Value(arguments: $"--inconclusive --project={NativeTracerProject.Path} --output-file={BuildDataDirectory}/{NativeTracerProject.Name}-cppcheck-{arch}.txt --enable=all --suppress=\"noExplicitConstructor\" --suppress=\"cstyleCast\" --suppress=\"duplicateBreak\" --suppress=\"unreadVariable\" --suppress=\"functionConst\" --suppress=\"funcArgNamesDifferent\" --suppress=\"variableScope\" --suppress=\"useStlAlgorithm\" --suppress=\"functionStatic\" --suppress=\"initializerList\" --suppress=\"redundantAssignment\" --suppress=\"redundantInitialization\" --suppress=\"shadowVariable\" --suppress=\"constParameter\" --suppress=\"unusedPrivateFunction\" --suppress=\"unusedFunction\" --suppress=\"missingInclude\" --suppress=\"unmatchedSuppression\" --suppress=\"knownConditionTrueFalse\"");
        });

    Target CppCheckNativeSrc => _ => _
        .Unlisted()
        .Description("Runs CppCheck over the native tracer project")
        .DependsOn(CppCheckNativeSrcUnix);

    Target CompileManagedSrc => _ => _
        .Unlisted()
        .Description("Compiles the managed code in the src directory")
        .After(CreateRequiredDirectories)
        .After(Restore)
        .Executes(() =>
        {
            var include = TracerDirectory.GlobFiles(
                "src/**/*.csproj"
            );

            var exclude = TracerDirectory.GlobFiles(
                "src/Datadog.Trace.Bundle/Datadog.Trace.Bundle.csproj",
                "src/Datadog.Trace.Tools.Runner/*.csproj",
                "src/**/Datadog.InstrumentedAssembly*.csproj"
            );

            var toBuild = include.Except(exclude);

            DotnetBuild(toBuild, noDependencies: false);
        });


    Target CompileTracerNativeTestsWindows => _ => _
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

    Target CompileTracerNativeTestsLinux => _ => _
        .Unlisted()
        .After(CompileNativeSrc)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");
            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target {FileNames.NativeTracerTests}");
        });

    Target CompileNativeTests => _ => _
        .Unlisted()
        .Description("Compiles the native unit tests (native loader, profiler)")
        .DependsOn(CompileTracerNativeTestsWindows)
        .DependsOn(CompileTracerNativeTestsLinux)
        .DependsOn(CompileNativeLoaderTestsWindows)
        .DependsOn(CompileNativeLoaderTestsLinux)
        .DependsOn(CompileProfilerNativeTestsWindows);

    Target DownloadLibDdwaf => _ => _.Unlisted().After(CreateRequiredDirectories).Executes(() => DownloadWafVersion());

    async Task DownloadWafVersion(string libddwafVersion = null, string uncompressFolderTarget = null)
    {
        var libDdwafUri = new Uri(
            $"https://www.nuget.org/api/v2/package/libddwaf/{libddwafVersion ?? LibDdwafVersion}"
        );
        var libDdwafZip = TempDirectory / "libddwaf.zip";

        using (var client = new HttpClient())
        {
            var response = await client.GetAsync(libDdwafUri);

            response.EnsureSuccessStatusCode();

            await using var file = File.Create(libDdwafZip);
            await using var stream = await response.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(file);
        }

        uncompressFolderTarget ??= LibDdwafDirectory(libddwafVersion);
        Console.WriteLine($"{libDdwafZip} downloaded. Extracting to {uncompressFolderTarget}...");

        UncompressZip(libDdwafZip, uncompressFolderTarget);
    }

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
                  var source = LibDdwafDirectory() / "runtimes" / architecture / "native" / "ddwaf.dll";
                  var dest = MonitoringHomeDirectory / architecture;
                  CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
              }
          }
          else if (IsLinux)
          {
              var (sourceArch, ext) = GetLibDdWafUnixArchitectureAndExtension();
              var (destArch, _) = GetUnixArchitectureAndExtension();

              var ddwafFileName = $"libddwaf.{ext}";

              var source = LibDdwafDirectory() / "runtimes" / sourceArch / "native" / ddwafFileName;
              var dest = MonitoringHomeDirectory / destArch;
              CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
          }
          else if (IsOsx)
          {
              var (sourceArch, ext) = GetLibDdWafUnixArchitectureAndExtension();
              var ddwafFileName = $"libddwaf.{ext}";

              var source = LibDdwafDirectory() / "runtimes" / sourceArch / "native" / ddwafFileName;
              var dest = MonitoringHomeDirectory / "osx";
              CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
          }
        });

    Target CopyNativeFilesForAppSecUnitTests => _ => _
                .Unlisted()
                .After(Clean)
                .After(DownloadLibDdwaf)
                .Executes(async () =>
                {
                    var project = Solution.GetProject(Projects.AppSecUnitTests);
                    var testDir = project.Directory;
                    var frameworks = project.GetTargetFrameworks();

                    var testBinFolder = testDir / "bin" / BuildConfiguration;

                    //older waf to test
                    var oldVersionTempPath = TempDirectory / $"libddwaf.{OlderLibDdwafVersion}";
                    Console.WriteLine("oldversion path is:" + oldVersionTempPath);
                    await DownloadWafVersion(OlderLibDdwafVersion, oldVersionTempPath);

                    // dotnet test runs under x86 for net461, even on x64 platforms
                    // so copy both, just to be safe
                    if (IsWin)
                    {
                        foreach (var arch in WafWindowsArchitectureFolders)
                        {
                            var oldVersionPath = oldVersionTempPath / "runtimes" / arch / "native" / "ddwaf.dll";
                            var source = MonitoringHomeDirectory / arch;
                            foreach (var fmk in frameworks)
                            {
                                var dest = testBinFolder / fmk / arch;
                                CopyDirectoryRecursively(source, dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);

                                CopyFile(oldVersionPath, dest / $"ddwaf-{OlderLibDdwafVersion}.dll", FileExistsPolicy.Overwrite);
                            }
                        }
                    }
                    else
                    {
                        var (arch, _) = GetUnixArchitectureAndExtension();
                        var (archWaf, ext) = GetLibDdWafUnixArchitectureAndExtension();
                        var source = MonitoringHomeDirectory / (IsOsx ? "osx" : arch);
                        var patchedArchWaf = (IsOsx ? archWaf + "-x64" : archWaf);
                        var oldVersionPath = oldVersionTempPath / "runtimes" / patchedArchWaf / "native" / $"libddwaf.{ext}";
                        foreach (var fmk in frameworks)
                        {
                            // We have to copy into the _root_ test bin folder here, not the arch sub-folder.
                            // This is because these tests try to load the WAF.
                            // Loading the WAF requires using the native tracer as a proxy, which means either
                            // - The native tracer must be loaded first, so it can rewrite the PInvoke calls
                            // - The native tracer must be side-by-side with the running dll
                            // As this is a managed-only unit test, the native tracer _must_ be in the root folder
                            // For simplicity, we just copy all the native dlls there
                            var dest = testBinFolder / fmk;

                            // use the files from the monitoring native folder
                            CopyDirectoryRecursively(source, dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);

                            CopyFile(oldVersionPath, dest / $"libddwaf-{OlderLibDdwafVersion}.{ext}", FileExistsPolicy.Overwrite);
                        }
                    }
                });

    Target PublishManagedTracer => _ => _
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
      .After(CompileNativeSrc, PublishManagedTracer)
      .Executes(() =>
       {
           foreach (var architecture in ArchitecturesForPlatformForTracer)
           {
               var source = NativeTracerProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                            $"{NativeTracerProject.Name}.pdb";
               var dest = SymbolsDirectory / $"win-{architecture}" / Path.GetFileName(source);
               CopyFile(source, dest, FileExistsPolicy.Overwrite);
           }
       });

    Target PublishNativeTracerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileNativeSrc, PublishManagedTracer)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatformForTracer)
            {
                // Copy native tracer assets
                var source = NativeTracerProject.Directory / "bin" / BuildConfiguration / architecture.ToString() /
                             $"{NativeTracerProject.Name}.dll";
                var dest = MonitoringHomeDirectory / $"win-{architecture}";
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
            }
        });

    Target PublishNativeTracerUnix => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileNativeSrc, PublishManagedTracer)
        .Executes(() =>
        {
            var (arch, extension) = GetUnixArchitectureAndExtension();

            // Copy Native file
            CopyFileToDirectory(
                NativeTracerProject.Directory / "build" / "bin" / $"{NativeTracerProject.Name}.{extension}",
                MonitoringHomeDirectory / arch,
                FileExistsPolicy.Overwrite);
        });

    Target PublishNativeTracerOsx => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsOsx)
        .After(CompileNativeSrc, PublishManagedTracer)
        .Executes(() =>
        {
            // Copy the universal binary to the output folder
            CopyFileToDirectory(
                NativeTracerProject.Directory / "build" / "bin" / $"{NativeTracerProject.Name}.dylib",
                MonitoringHomeDirectory / "osx",
                FileExistsPolicy.Overwrite,
                true);
        });

    Target PublishNativeTracer => _ => _
        .Unlisted()
        .DependsOn(PublishNativeTracerWindows)
        .DependsOn(PublishNativeTracerUnix)
        .DependsOn(PublishNativeTracerOsx);

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
                    .CombineWith(ArchitecturesForPlatformForTracer, (o, arch) => o
                        .SetProperty("MsiOutputPath", ArtifactsDirectory / arch.ToString())
                        .SetTargetPlatform(arch)),
                degreeOfParallelism: 2);
        });

    Target CreateBundleHome => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .Executes(() =>
        {
            // clean directory of everything except the text files
            BundleHomeDirectory
               .GlobFiles("*.*")
               .Where(filepath => Path.GetExtension(filepath) != ".txt")
               .ForEach(DeleteFile);

            // Copy existing files from tracer home to the Bundle location
            CopyDirectoryRecursively(MonitoringHomeDirectory, BundleHomeDirectory, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);

            // Add the create log path script
            CopyFileToDirectory(BuildDirectory / "artifacts" / FileNames.CreateLogPathScript, BundleHomeDirectory);
        });

    Target ExtractDebugInfoLinux => _ => _
        .Unlisted()
        .After(BuildProfilerHome, BuildTracerHome, BuildNativeLoader)
        .Executes(() =>
        {
            // extract debug info from everything in monitoring home and copy it to the linux symbols directory
            var files = MonitoringHomeDirectory.GlobFiles("linux-*/*.so");

            foreach (var file in files)
            {
                var outputDir = SymbolsDirectory / new FileInfo(file).Directory!.Name;
                EnsureExistingDirectory(outputDir);
                var outputFile = outputDir / Path.GetFileNameWithoutExtension(file);

                Logger.Info($"Extracting debug symbol for {file} to {outputFile}.debug");
                ExtractDebugInfo.Value(arguments: $"--only-keep-debug {file} {outputFile}.debug");

                Logger.Info($"Stripping out unneeded information from {file}");
                StripBinary.Value(arguments: $"--strip-unneeded {file}");
            }
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
       .DependsOn(ZipMonitoringHomeWindows)
       .DependsOn(ZipMonitoringHomeLinux)
       .DependsOn(ZipMonitoringHomeOsx);

    Target ZipMonitoringHomeWindows => _ => _
        .Unlisted()
        .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            CompressZip(MonitoringHomeDirectory, WindowsTracerHomeZip, fileMode: FileMode.Create);
        });

    Target PrepareMonitoringHomeLinux => _ => _
        .Unlisted()
        .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader)
        .OnlyWhenStatic(() => IsLinux)
        .Requires(() => Version)
        .Executes(() =>
        {
            var fpm = Fpm.Value;
            var gzip = GZip.Value;
            var chmod = Chmod.Value;

            // For legacy back-compat reasons, we _must_ add certain files to their expected locations
            // in the linux packages, as customers may have environment variables pointing to them
            // we do this work in the temp folder to avoid "messing" with the artifacts directory
            var (arch, ext) = GetUnixArchitectureAndExtension();
            var assetsDirectory = TemporaryDirectory / arch;
            EnsureCleanDirectory(assetsDirectory);
            CopyDirectoryRecursively(MonitoringHomeDirectory, assetsDirectory, DirectoryExistsPolicy.Merge);

            // For back-compat reasons, we must always have the Datadog.ClrProfiler.Native.so file in the root folder
            // as it's set in the COR_PROFILER_PATH etc env var
            // so create a symlink to avoid bloating package sizes
            var archSpecificFile = assetsDirectory / arch / $"{FileNames.NativeLoader}.{ext}";
            var linkLocation = assetsDirectory / $"{FileNames.NativeLoader}.{ext}";
            HardLinkUtil.Value($"-v {archSpecificFile} {linkLocation}");

            // For back-compat reasons, we have to keep the libddwaf.so file in the root folder
            // because the way AppSec probes the paths won't find the linux-musl-x64 target currently
            archSpecificFile = assetsDirectory / arch / FileNames.AppSecLinuxWaf;
            linkLocation = assetsDirectory / FileNames.AppSecLinuxWaf;
            HardLinkUtil.Value($"-v {archSpecificFile} {linkLocation}");

            // we must always have the Datadog.Linux.ApiWrapper.x64.so file in the continuousprofiler subfolder
            // as it's set in the LD_PRELOAD env var
            var continuousProfilerDir = assetsDirectory / "continuousprofiler";
            EnsureExistingDirectory(continuousProfilerDir);
            archSpecificFile = assetsDirectory / arch / FileNames.ProfilerLinuxApiWrapper;
            linkLocation = continuousProfilerDir / FileNames.ProfilerLinuxApiWrapper;
            HardLinkUtil.Value($"-v {archSpecificFile} {linkLocation}");

            // Copy the loader.conf to the root folder, this is required for when the "root" native loader is used,
            // It needs to include the architecture in the paths to the native dlls
            //
            // The regex replaces (for example):
            //      PROFILER;{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A};linux-x64;./Datadog.Profiler.Native.so
            // with (adds folder prefix):
            //      PROFILER;{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A};linux-x64;./linux-x64/Datadog.Profiler.Native.so
            var loaderConfContents = File.ReadAllText(MonitoringHomeDirectory / arch / FileNames.LoaderConf);
            loaderConfContents = Regex.Replace(
                input: loaderConfContents,
                pattern: @";(linux-.*?);\.\/Datadog\.",
                replacement:$@";$1;./{arch}/Datadog.");
            File.WriteAllText(assetsDirectory / FileNames.LoaderConf, contents: loaderConfContents);

            // Copy createLogPath.sh script and set the permissions
            CopyFileToDirectory(BuildDirectory / "artifacts" / FileNames.CreateLogPathScript, assetsDirectory);
            chmod.Invoke($"+x {assetsDirectory / FileNames.CreateLogPathScript}");
        });

    Target ZipMonitoringHomeLinux => _ => _
        .Unlisted()
        .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader)
        .DependsOn(PrepareMonitoringHomeLinux)
        .OnlyWhenStatic(() => IsLinux)
        .Requires(() => Version)
        .Executes(() =>
        {
            var fpm = Fpm.Value;
            var gzip = GZip.Value;

            var (arch, ext) = GetUnixArchitectureAndExtension();
            var assetsDirectory = TemporaryDirectory / arch;
            var workingDirectory = ArtifactsDirectory / $"linux-{UnixArchitectureIdentifier}";
            EnsureCleanDirectory(workingDirectory);

            const string packageName = "datadog-dotnet-apm";
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
                    $"--chdir {assetsDirectory}",
                    "createLogPath.sh",
                    "netstandard2.0/",
                    "netcoreapp3.1/",
                    "net6.0/",
                    "Datadog.Trace.ClrProfiler.Native.so",
                    "libddwaf.so",
                    "continuousprofiler/",
                    "loader.conf",
                    $"{arch}/",
                };

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
        });

    Target ZipMonitoringHomeOsx => _ => _
        .Unlisted()
        .After(BuildTracerHome, BuildNativeLoader)
        .OnlyWhenStatic(() => IsOsx)
        .Executes(() =>
        {
            // As a naive approach let's do the same as windows, create a zip folder
            CompressZip(MonitoringHomeDirectory, OsxTracerHomeZip, fileMode: FileMode.Create);
        });

    Target CompileInstrumentationVerificationLibrary => _ => _
        .Unlisted()
        .After(Restore, CompileManagedSrc)
        .Executes(() =>
        {
            DotnetBuild(TracerDirectory.GlobFiles("src/**/Datadog.InstrumentedAssembly*.csproj"), noDependencies: false);
        });

    Target CompileManagedTestHelpers => _ => _
        .Unlisted()
        .After(Restore)
        .After(CompileManagedSrc)
        .DependsOn(CompileInstrumentationVerificationLibrary)
        .Executes(() =>
        {
            //we need to build in this exact order
            DotnetBuild(TracerDirectory.GlobFiles("test/**/*TestHelpers.csproj"));
            DotnetBuild(TracerDirectory.GlobFiles("test/**/*TestHelpers.AutoInstrumentation.csproj"));
        });

    Target CompileManagedUnitTests => _ => _
        .Unlisted()
        .After(Restore)
        .After(CompileManagedSrc)
        .After(BuildRunnerTool)
        .DependsOn(CopyNativeFilesForAppSecUnitTests)
        .DependsOn(CompileManagedTestHelpers)
        .Executes(() =>
        {
            DotnetBuild(TracerDirectory.GlobFiles("test/**/*.Tests.csproj"));
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
            var filter = string.IsNullOrEmpty(Filter) && IsArm64 ? "(Category!=ArmUnsupported)&(Category!=AzureFunctions)" : Filter;
            var exceptions = new List<Exception>();
            try
            {
                foreach (var targetFramework in TestingFrameworks.Where(x => x == Framework || Framework is null))
                {
                    try
                    {
                        DotNetTest(x => x
                            .EnableNoRestore()
                            .EnableNoBuild()
                            .SetFilter(filter)
                            .SetConfiguration(BuildConfiguration)
                            .SetTargetPlatformAnyCPU()
                            .SetDDEnvironmentVariables("dd-tracer-dotnet")
                            .SetFramework(targetFramework)
                            .EnableCrashDumps()
                            .SetLogsDirectory(TestLogsDirectory)
                            .When(CodeCoverage, ConfigureCodeCoverage)
                            .When(!string.IsNullOrEmpty(Filter), c => c.SetFilter(Filter))
                            .CombineWith(testProjects, (x, project) => x
                                .EnableTrxLogOutput(GetResultsDirectory(project))
                                .WithDatadogLogger()
                                .SetProjectFile(project)));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error testing {targetFramework}");
                        exceptions.Add(ex);
                    }
                }

                if (exceptions.Any())
                {
                    throw new AggregateException("Error in one or more test runs", exceptions);
                }
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target RunTracerNativeTestsWindows => _ => _
        .Unlisted()
        .After(CompileTracerNativeTestsWindows)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            var workingDirectory = TestsDirectory / "Datadog.Tracer.Native.Tests" / "bin" / BuildConfiguration.ToString() / TargetPlatform.ToString();
            var exePath = workingDirectory / "Datadog.Tracer.Native.Tests.exe";
            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe("--gtest_output=xml", workingDirectory: workingDirectory);
        });

    Target RunTracerNativeTestsLinux => _ => _
        .Unlisted()
        .After(CompileTracerNativeTestsLinux)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var workingDirectory = TestsDirectory / FileNames.NativeTracerTests / "bin";
            EnsureExistingDirectory(workingDirectory);

            var exePath = workingDirectory / FileNames.NativeTracerTests;
            Chmod.Value.Invoke("+x " + exePath);

            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe("--gtest_output=xml", workingDirectory: workingDirectory);
        });

    Target RunNativeTests => _ => _
        .Unlisted()
        .DependsOn(RunTracerNativeTestsWindows)
        .DependsOn(RunTracerNativeTestsLinux)
        .DependsOn(RunNativeLoaderTestsWindows)
        .DependsOn(RunNativeLoaderTestsLinux)
        .DependsOn(RunProfilerNativeUnitTestsWindows)
        .DependsOn(RunProfilerNativeUnitTestsLinux);

    Target CompileDependencyLibs => _ => _
        .Unlisted()
        .After(Restore)
        .After(CompileManagedSrc)
        .Executes(() =>
        {
            var projects = TracerDirectory.GlobFiles(
                "test/test-applications/integrations/dependency-libs/**/*.csproj",
                "test/test-applications/integrations/**/*.vbproj"
            );

            DotnetBuild(projects, noDependencies: false);
        });

    Target CompileRegressionDependencyLibs => _ => _
        .Unlisted()
        .After(Restore)
        .After(CompileManagedSrc)
        .Executes(() =>
        {
            var projects = TracerDirectory.GlobFiles(
                "test/test-applications/regression/dependency-libs/**/Datadog.StackExchange.Redis*.csproj"
            );

            DotnetBuild(projects, noDependencies: false);
        });

    Target CompileRegressionSamples => _ => _
        .Unlisted()
        .After(Restore)
        .After(CompileRegressionDependencyLibs)
        .Requires(() => Framework)
        .Executes(() =>
        {
            var regressionLibs = Solution.GetProject(Projects.DataDogThreadTest).Directory.Parent
                .GlobFiles("**/*.csproj")
                .Where(absPath =>
                {
                    var path = absPath.ToString();
                    return (path, Solution.GetProject(path).TryGetTargetFrameworks()) switch
                    {
                        _ when path.Contains("ExpenseItDemo") => false,
                        _ when path.Contains("StackExchange.Redis.AssemblyConflict.LegacyProject") => false,
                        _ when path.Contains("MismatchedTracerVersions") => false,
                        _ when path.Contains("dependency-libs") => false,
                        _ when !string.IsNullOrWhiteSpace(SampleName) => path.Contains(SampleName),
                        (_, { } targets) => targets.Contains(Framework),
                        _ => true,
                    };
                });

            // Allow restore here, otherwise things go wonky with runtime identifiers
            // in some target frameworks. No, I don't know why
            DotnetBuild(regressionLibs, framework: Framework, noRestore: false);
        });

    Target CompileFrameworkReproductions => _ => _
        .Unlisted()
        .Description("Builds .NET Framework projects (non SDK-based projects)")
        .After(CompileRegressionDependencyLibs)
        .After(CompileDependencyLibs)
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
                .SetTargetPlatformAnyCPU()
                .SetTargets("BuildFrameworkReproductions")
                .SetMaxCpuCount(null));
        });

    Target CompileIntegrationTests => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileManagedTestHelpers)
        .After(CompileRegressionSamples)
        .After(CompileFrameworkReproductions)
        .After(PublishIisSamples)
        .After(BuildRunnerTool)
        .Requires(() => Framework)
        .Requires(() => MonitoringHomeDirectory != null)
        .Executes(() =>
        {
            if (!Framework.ToString().StartsWith("net46"))
            {
                // we need to build RazorPages before integration tests for .net46x
                DotnetBuild(Solution.GetProject(Projects.RazorPages), framework: Framework);
            }

            var projects = TracerDirectory
                    .GlobFiles("test/*.IntegrationTests/*.IntegrationTests.csproj")
                    .Where(path => !((string)path).Contains(Projects.DebuggerIntegrationTests))
                    .Where(project => Solution.GetProject(project).GetTargetFrameworks().Contains(Framework))
                ;

            DotnetBuild(projects, framework: Framework);
        });

    Target CompileSamplesWindows => _ => _
        .Unlisted()
        .After(CompileDependencyLibs)
        .After(CompileFrameworkReproductions)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Framework)
        .Executes(() =>
        {
            // This does some "unnecessary" rebuilding and restoring
            var includeIntegration = TracerDirectory.GlobFiles("test/test-applications/integrations/**/*.csproj");
            // Don't build aspnet full framework sample in this step
            var includeSecurity = TracerDirectory.GlobFiles("test/test-applications/security/*/*.csproj");

            var exclude = TracerDirectory.GlobFiles("test/test-applications/integrations/dependency-libs/**/*.csproj")
                                         .Concat(TracerDirectory.GlobFiles("test/test-applications/debugger/dependency-libs/**/*.csproj"));

            var projects = includeIntegration
                .Concat(includeSecurity)
                .Select(x => Solution.GetProject(x))
                .Where(project =>
                (project, project.TryGetTargetFrameworks(), project.RequiresDockerDependency()) switch
                {
                    _ when exclude.Contains(project.Path) => false,
                    _ when !string.IsNullOrWhiteSpace(SampleName) => project.Path.ToString().Contains(SampleName),
                    (_, _, true) => false, // can't use docker on Windows
                    (_, { } targets, _) => targets.Contains(Framework),
                    _ => true,
                }
            );

            // /nowarn:NU1701 - Package 'x' was restored using '.NETFramework,Version=v4.6.1' instead of the project target framework '.NETCoreApp,Version=v2.1'.
            DotNetBuild(config => config
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatformAnyCPU()
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
                .SetTargetPlatformAnyCPU()
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
        .Triggers(PrintSnapshotsDiff)
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
                    .SetTargetPlatformAnyCPU()
                    .SetFramework(Framework)
                    //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                    .EnableCrashDumps()
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(!string.IsNullOrEmpty(Filter), c => c.SetFilter(Filter))
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ParallelIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .WithDatadogLogger()
                        .SetProjectFile(project)), degreeOfParallelism: 4);


                // TODO: I think we should change this filter to run on Windows by default
                // (RunOnWindows!=False|Category=Smoke)&LoadFromGAC!=True&IIS!=True
                DotNetTest(config => config
                    .SetDotnetPath(TargetPlatform)
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatformAnyCPU()
                    .SetFramework(Framework)
                    //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(Filter ?? "RunOnWindows=True&LoadFromGAC!=True&IIS!=True&Category!=AzureFunctions")
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ClrProfilerIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .WithDatadogLogger()
                        .SetProjectFile(project)));
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target CompileAzureFunctionsSamplesWindows => _ => _
        .Unlisted()
        .After(CompileFrameworkReproductions)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Framework)
        .Executes(() =>
        {
            // This does some "unnecessary" rebuilding and restoring
            var azureFunctions = TracerDirectory.GlobFiles("test/test-applications/azure-functions/**/*.csproj");

            var projects = azureFunctions
                .Where(path =>
                {
                    var project = Solution.GetProject(path);
                    return (project, project.TryGetTargetFrameworks(), project.RequiresDockerDependency()) switch
                    {
                        (_, { } targets, _) => targets.Contains(Framework),
                        _ => true,
                    };
                });


            DotnetBuild(projects, noRestore: false);
        });

    Target RunWindowsAzureFunctionsTests => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .After(CompileIntegrationTests)
        .After(CompileAzureFunctionsSamplesWindows)
        .After(BuildWindowsIntegrationTests)
        .Requires(() => IsWin)
        .Requires(() => Framework)
        .Triggers(PrintSnapshotsDiff)
        .Executes(() =>
        {
            var project = Solution.GetProject(Projects.ClrProfilerIntegrationTests);
            EnsureExistingDirectory(TestLogsDirectory);
            EnsureResultsDirectory(project);

            try
            {
                DotNetTest(config => config
                    .SetDotnetPath(TargetPlatform)
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatformAnyCPU()
                    .SetFramework(Framework)
                    //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(Filter ?? "RunOnWindows=True&Category=AzureFunctions")
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .EnableTrxLogOutput(GetResultsDirectory(project))
                    .WithDatadogLogger()
                    .SetProjectFile(project));
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
                    .SetTargetPlatformAnyCPU()
                    .SetFramework(Framework)
                    .EnableCrashDumps()
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(Filter ?? "Category=Smoke&LoadFromGAC!=True&Category!=AzureFunctions")
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ClrProfilerIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .WithDatadogLogger()
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
        .Triggers(PrintSnapshotsDiff)
        .Requires(() => Framework)
        .Executes(() => RunWindowsIisIntegrationTests(
                      Solution.GetProject(Projects.ClrProfilerIntegrationTests)));

    Target RunWindowsSecurityIisIntegrationTests => _ => _
        .After(BuildTracerHome)
        .After(CompileIntegrationTests)
        .After(CompileFrameworkReproductions)
        .After(PublishIisSamples)
        .Triggers(PrintSnapshotsDiff)
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
                                .SetTargetPlatformAnyCPU()
                                .SetFramework(Framework)
                                .EnableNoRestore()
                                .EnableNoBuild()
                                .SetFilter(Filter ?? "(RunOnWindows=True)&LoadFromGAC=True&Category!=AzureFunctions")
                                .SetTestTargetPlatform(TargetPlatform)
                                .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                                .SetLogsDirectory(TestLogsDirectory)
                                .When(CodeCoverage, ConfigureCodeCoverage)
                                .EnableTrxLogOutput(GetResultsDirectory(project))
                                .WithDatadogLogger()
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
        .Triggers(PrintSnapshotsDiff)
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
                    .SetTargetPlatformAnyCPU()
                    .SetFramework(Framework)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFilter(Filter ?? "(RunOnWindows=True)&MSI=True&Category!=AzureFunctions")
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .EnableTrxLogOutput(resultsDirectory)
                    .WithDatadogLogger()
                    .SetProjectFile(project));
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target CompileSamplesLinuxOrOsx => _ => _
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
                .Select(path => (path, project: Solution.GetProject(path)))
                .Where(x => (IncludeTestsRequiringDocker, x.project) switch
                {
                    // filter out or to integration tests that have docker dependencies
                    (null, _) => true,
                    (_, null) => true,
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
                        "LogsInjection.NLog10.VersionConflict.2x" => Framework == TargetFramework.NET461 || Framework == TargetFramework.NET462,
                        "LogsInjection.NLog20.VersionConflict.2x" => Framework == TargetFramework.NET461 || Framework == TargetFramework.NET462,
                        "LogsInjection.Serilog.VersionConflict.2x" => Framework != TargetFramework.NETCOREAPP2_1,
                        "LogsInjection.Serilog14.VersionConflict.2x" => Framework == TargetFramework.NET461 || Framework == TargetFramework.NET462,
                        "Samples.AspNetCoreMvc21" => Framework == TargetFramework.NETCOREAPP2_1,
                        "Samples.AspNetCoreMvc30" => Framework == TargetFramework.NETCOREAPP3_0,
                        "Samples.AspNetCoreMvc31" => Framework == TargetFramework.NETCOREAPP3_1,
                        "Samples.AspNetCoreMinimalApis" => Framework == TargetFramework.NET7_0 || Framework == TargetFramework.NET6_0,
                        "Samples.Security.AspNetCore2" => Framework == TargetFramework.NETCOREAPP2_1,
                        "Samples.Security.AspNetCore5" => Framework == TargetFramework.NET7_0 || Framework == TargetFramework.NET6_0 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NETCOREAPP3_0,
                        "Samples.Security.AspNetCoreBare" => Framework == TargetFramework.NET7_0 || Framework == TargetFramework.NET6_0 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NETCOREAPP3_0,
                        "Samples.GraphQL4" => Framework == TargetFramework.NET7_0 || Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NET6_0,
                        "Samples.GraphQL7" => Framework == TargetFramework.NET7_0 || Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NET6_0,
                        "Samples.HotChocolate" => Framework == TargetFramework.NET7_0 || Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NET6_0,
                        "Samples.AWS.Lambda" => Framework == TargetFramework.NETCOREAPP3_1 || Framework == TargetFramework.NET5_0 || Framework == TargetFramework.NET6_0 || Framework == TargetFramework.NET7_0,
                        var name when projectsToSkip.Contains(name) => false,
                        var name when multiPackageProjects.Contains(name) => false,
                        "Samples.AspNetCoreRazorPages" => true,
                        _ when !string.IsNullOrWhiteSpace(SampleName) => x.project?.Name?.Contains(SampleName) ?? false,
                        _ => true,
                    };
                })
                .Select(x => x.path)
                .ToArray();

            // do the build and publish separately to avoid dependency issues

            DotnetBuild(projectsToBuild, framework: Framework, noRestore: false);

            DotNetPublish(x => x
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .EnableNoDependencies()
                    .SetConfiguration(BuildConfiguration)
                    .SetFramework(Framework)
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
        .After(CompileSamplesLinuxOrOsx)
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

    Target CompileLinuxOrOsxIntegrationTests => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileRegressionDependencyLibs)
        .After(CompileDependencyLibs)
        .After(CompileManagedTestHelpers)
        .After(CompileSamplesLinuxOrOsx)
        .After(CompileMultiApiPackageVersionSamples)
        .After(BuildRunnerTool)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Framework)
        .Executes(() =>
        {
            // Build the actual integration test projects for Any CPU
            var integrationTestProjects =
                TracerDirectory
                   .GlobFiles("test/*.IntegrationTests/*.csproj")
                   .Where(path => !((string)path).Contains(Projects.DebuggerIntegrationTests));

            DotnetBuild(integrationTestProjects, framework: Framework, noRestore: false);

            IntegrationTestLinuxOrOsxProfilerDirFudge(Projects.ClrProfilerIntegrationTests);
            IntegrationTestLinuxOrOsxProfilerDirFudge(Projects.AppSecIntegrationTests);
        });

    Target RunLinuxIntegrationTests => _ => _
        .After(CompileLinuxOrOsxIntegrationTests)
        .Description("Runs the linux integration tests")
        .Requires(() => Framework)
        .Requires(() => !IsWin)
        .Triggers(PrintSnapshotsDiff)
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

            var armFilter = IsArm64 ? "&(Category!=ArmUnsupported)" : string.Empty;

            var filter = string.IsNullOrEmpty(Filter) switch
            {
                false => Filter,
                true => $"(Category!=LinuxUnsupported)&(Category!=Lambda)&(Category!=AzureFunctions){dockerFilter}{armFilter}",
            };

            try
            {
                // Run these ones in parallel
                DotNetTest(config => config
                        .SetConfiguration(BuildConfiguration)
                        .EnableNoRestore()
                        .EnableNoBuild()
                        .SetFramework(Framework)
                        //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                        .EnableCrashDumps()
                        .SetFilter(filter)
                        .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                        .SetTestTargetPlatform(TargetPlatform)
                        .SetLogsDirectory(TestLogsDirectory)
                        .When(TestAllPackageVersions, o => o.SetProcessEnvironmentVariable("TestAllPackageVersions", "true"))
                        .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                        .When(IncludeTestsRequiringDocker is not null, o => o.SetProperty("IncludeTestsRequiringDocker", IncludeTestsRequiringDocker.Value ? "true" : "false"))
                        .When(CodeCoverage, ConfigureCodeCoverage)
                        .CombineWith(ParallelIntegrationTests, (s, project) => s
                            .EnableTrxLogOutput(GetResultsDirectory(project))
                            .WithDatadogLogger()
                            .SetProjectFile(project)),
                    degreeOfParallelism: 2);

                // Run this one separately so we can tail output
                DotNetTest(config => config
                    .SetConfiguration(BuildConfiguration)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFramework(Framework)
					 //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                    .EnableCrashDumps()
                    .SetFilter(filter)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(TestAllPackageVersions, o => o.SetProcessEnvironmentVariable("TestAllPackageVersions", "true"))
                    .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                    .When(IncludeTestsRequiringDocker is not null, o => o.SetProperty("IncludeTestsRequiringDocker", IncludeTestsRequiringDocker.Value ? "true" : "false"))
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ClrProfilerIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .WithDatadogLogger()
                        .SetProjectFile(project))
                );
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target RunOsxIntegrationTests => _ => _
        .After(CompileLinuxOrOsxIntegrationTests)
        .Description("Runs the osx integration tests")
        .Requires(() => Framework)
        .Requires(() => IsOsx)
        .Triggers(PrintSnapshotsDiff)
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

            var armFilter = IsArm64 ? "&(Category!=ArmUnsupported)" : string.Empty;

            var filter = string.IsNullOrEmpty(Filter) switch
            {
                false => Filter,
                true => $"(Category!=LinuxUnsupported)&(Category!=Lambda)&(Category!=AzureFunctions){dockerFilter}{armFilter}",
            };

            var targetPlatform = IsArm64 ? (MSBuildTargetPlatform) "arm64" : TargetPlatform;

            try
            {
                // Run these ones in parallel
                DotNetTest(config => config
                        .SetConfiguration(BuildConfiguration)
                        .EnableNoRestore()
                        .EnableNoBuild()
                        .SetFramework(Framework)
                        //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                        .EnableCrashDumps()
                        .SetFilter(filter)
                        .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                        .SetLocalOsxEnvironmentVariables()
                        .SetTestTargetPlatform(targetPlatform)
                        .SetLogsDirectory(TestLogsDirectory)
                        .When(TestAllPackageVersions, o => o.SetProcessEnvironmentVariable("TestAllPackageVersions", "true"))
                        .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                        .When(IncludeTestsRequiringDocker is not null, o => o.SetProperty("IncludeTestsRequiringDocker", IncludeTestsRequiringDocker.Value ? "true" : "false"))
                        .When(CodeCoverage, ConfigureCodeCoverage)
                        .CombineWith(ParallelIntegrationTests, (s, project) => s
                            .EnableTrxLogOutput(GetResultsDirectory(project))
                            .WithDatadogLogger()
                            .SetProjectFile(project)),
                    degreeOfParallelism: 2);

                // Run this one separately so we can tail output
                DotNetTest(config => config
                    .SetConfiguration(BuildConfiguration)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .SetFramework(Framework)
                    //.WithMemoryDumpAfter(timeoutInMinutes: 30)
                    .EnableCrashDumps()
                    .SetFilter(filter)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLocalOsxEnvironmentVariables()
                    .SetTestTargetPlatform(targetPlatform)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(TestAllPackageVersions, o => o.SetProcessEnvironmentVariable("TestAllPackageVersions", "true"))
                    .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                    .When(IncludeTestsRequiringDocker is not null, o => o.SetProperty("IncludeTestsRequiringDocker", IncludeTestsRequiringDocker.Value ? "true" : "false"))
                    .When(CodeCoverage, ConfigureCodeCoverage)
                    .CombineWith(ClrProfilerIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .WithDatadogLogger()
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
              DotnetBuild(Solution.GetProject(Projects.ToolArtifactsTests));
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
                .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                .SetProcessEnvironmentVariable("ToolInstallDirectory", ToolInstallDirectory)
                .SetLogsDirectory(TestLogsDirectory)
                .EnableTrxLogOutput(GetResultsDirectory(project))
                .WithDatadogLogger());
        });

    Target CopyServerlessArtifacts => _ => _
       .Description("Copies monitoring-home into the serverless artifacts directory")
       .Unlisted()
       .After(CompileSamplesLinuxOrOsx, CompileMultiApiPackageVersionSamples)
       .Executes(() =>
        {

            var projectFile = TracerDirectory.GlobFiles("test/test-applications/integrations/*/Samples.AWS.Lambda.csproj").FirstOrDefault();
            var target = projectFile / ".." / "bin" / "artifacts" / "monitoring-home";

            CopyDirectoryRecursively(MonitoringHomeDirectory, target, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
        });


    Target CheckBuildLogsForErrors => _ => _
       .Unlisted()
       .Description("Reads the logs from build_data and checks for error lines")
       .Executes(() =>
       {
           // we expect to see _some_ errors, so explicitly ignore them
           var knownPatterns = new List<Regex>
           {
               new(@".*Unable to resolve method MongoDB\..*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsIntegration\.OnAsyncMethodEnd.*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsIntegration\.OnMethodBegin.*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsIntegration\.OnMethodEnd.*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsVoidIntegration\.OnMethodBegin.*", RegexOptions.Compiled),
               new(@".*at CallTargetNativeTest\.NoOp\.Noop\dArgumentsVoidIntegration\.OnMethodEnd.*", RegexOptions.Compiled),
               new(@".*System.Threading.ThreadAbortException: Thread was being aborted\.", RegexOptions.Compiled),
               // CI Visibility known errors
               new (@".*The Git repository couldn't be automatically extracted.*", RegexOptions.Compiled),
               new (@".*DD_GIT_REPOSITORY_URL is set with.*", RegexOptions.Compiled),
               new (@".*The Git commit sha couldn't be automatically extracted.*", RegexOptions.Compiled),
               new (@".*DD_GIT_COMMIT_SHA must be a full-length git SHA.*", RegexOptions.Compiled),
               new (@".*Timeout occurred when flushing spans.*", RegexOptions.Compiled),
               new (@".*ITR: .*", RegexOptions.Compiled),
               // This one is annoying but we _think_ due to a dodgy named pipes implementation, so ignoring for now
               new(@".*An error occurred while sending data to the agent at \\\\\.\\pipe\\trace-.*The operation has timed out.*", RegexOptions.Compiled),
               new(@".*An error occurred while sending data to the agent at \\\\\.\\pipe\\metrics-.*The operation has timed out.*", RegexOptions.Compiled),
           };

           CheckLogsForErrors(knownPatterns, allFilesMustExist: false, minLogLevel: LogLevel.Error);
       });

    Target CheckSmokeTestsForErrors => _ => _
       .Unlisted()
       .Description("Reads the logs from build_data and checks for error lines in the smoke test logs")
       .Executes(() =>
       {
           var knownPatterns = new List<Regex>();

           if (IsAlpine)
           {
               // AppSec complains about not loading initially on alpine, but can be ignored
               knownPatterns.Add(new(@".*'dddlopen' dddlerror returned: Library linux-vdso\.so\.1 is not already loaded", RegexOptions.Compiled));
           }

           if (IsArm64)
           {
               // Profiler is not yet supported on Arm64
               knownPatterns.Add(new(@".*Profiler is deactivated because it runs on an unsupported architecture", RegexOptions.Compiled));
               knownPatterns.Add(new(@".*Error getting IClassFactory from: .*/Datadog\.Profiler\.Native\.so", RegexOptions.Compiled));
               knownPatterns.Add(new(@".*DynamicDispatcherImpl::LoadClassFactory: Error trying to load continuous profiler class factory.*", RegexOptions.Compiled));
               knownPatterns.Add(new(@".*Error loading all cor profiler class factories\.", RegexOptions.Compiled));
           }

            // profiler occasionally throws this if shutting down
            knownPatterns.Add(new(@".*LinuxStackFramesCollector::CollectStackSampleImplementation: Unable to send signal .*Error code: No such process", RegexOptions.Compiled));
            // profiler throws this on .NET 7 - currently allowed
            knownPatterns.Add(new(@".*Profiler call failed with result Unspecified-Failure \(80131351\): pInfo..GetModuleInfo\(moduleId, nullptr, 0, nullptr, nullptr, .assemblyId\)", RegexOptions.Compiled));

           CheckLogsForErrors(knownPatterns, allFilesMustExist: true, minLogLevel: LogLevel.Warning);
       });

    private void CheckLogsForErrors(List<Regex> knownPatterns, bool allFilesMustExist, LogLevel minLogLevel)
    {
        var logDirectory = BuildDataDirectory / "logs";
        if (!DirectoryExists(logDirectory))
        {
            Logger.Info($"Skipping log parsing, directory '{logDirectory}' not found");
            if (allFilesMustExist)
            {
                ExitCode = 1;
                return;
            }
        }

        var managedFiles = logDirectory.GlobFiles("**/dotnet-tracer-managed-*");
        var managedErrors = managedFiles
                           .SelectMany(ParseManagedLogFiles)
                           .Where(IsProblematic)
                           .ToList<ParsedLogLine>();

        var nativeTracerFiles = logDirectory.GlobFiles("**/dotnet-tracer-native-*");
        var nativeTracerErrors = nativeTracerFiles
                                .SelectMany(ParseNativeTracerLogFiles)
                                .Where(IsProblematic)
                                .ToList();

        var nativeProfilerFiles = logDirectory.GlobFiles("**/DD-DotNet-Profiler-Native-*");
        var nativeProfilerErrors = nativeProfilerFiles
                                  .SelectMany(ParseNativeProfilerLogFiles)
                                  .Where(IsProblematic)
                                  .ToList();

        var nativeLoaderFiles = logDirectory.GlobFiles("**/dotnet-native-loader-*");
        var nativeLoaderErrors = nativeLoaderFiles
                                  .SelectMany(ParseNativeProfilerLogFiles) // native loader has same format as profiler
                                  .Where(IsProblematic)
                                  .ToList();

        var hasRequiredFiles = !allFilesMustExist
                            || (managedFiles.Count > 0
                             && nativeTracerFiles.Count > 0
                             && nativeProfilerFiles.Count > 0
                             && nativeLoaderFiles.Count > 0);

        if (hasRequiredFiles
         && managedErrors.Count == 0
         && nativeTracerErrors.Count == 0
         && nativeProfilerErrors.Count == 0
         && nativeLoaderErrors.Count == 0)
        {
            Logger.Info("No problems found in managed or native logs");
            return;
        }

        Logger.Warn("Found the following problems in log files:");
        var allErrors = managedErrors
                       .Concat(nativeTracerErrors)
                       .Concat(nativeProfilerErrors)
                       .Concat(nativeLoaderErrors)
                       .GroupBy(x => x.FileName);

        foreach (var erroredFile in allErrors)
        {
            var errors = erroredFile.Where(x => !ContainsCanary(x)).ToList();
            if(errors.Any())
            {
                Logger.Info();
                Logger.Error($"Found errors in log file '{erroredFile.Key}':");
                foreach (var error in errors)
                {
                    Logger.Error($"{error.Timestamp:hh:mm:ss} [{error.Level}] {error.Message}");
                }
            }

            var canaries = erroredFile.Where(ContainsCanary).ToList();
            if(canaries.Any())
            {
                Logger.Info();
                Logger.Error($"Found usage of canary environment variable in log file '{erroredFile.Key}':");
                foreach (var canary in canaries)
                {
                    Logger.Error($"{canary.Timestamp:hh:mm:ss} [{canary.Level}] {canary.Message}");
                }
            }
        }

        ExitCode = 1;

        bool IsProblematic(ParsedLogLine logLine)
        {
            if(ContainsCanary(logLine))
            {
                return true;
            }

            if(logLine.Level < minLogLevel)
            {
                return false;
            }

            foreach (var pattern in knownPatterns)
            {
                if (pattern.IsMatch(logLine.Message))
                {
                    return false;
                }
            }

            return true;
        }

        bool ContainsCanary(ParsedLogLine logLine)
            => logLine.Message.Contains("SUPER_SECRET_CANARY")
                || logLine.Message.Contains("MySuperSecretCanary");

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
                        currentLine = null;
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

            if (currentLine is not null)
            {
                allLogs.Add(currentLine);
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
                        currentLine = null;
                    }

                    try
                    {
                        // native logs are on one line
                        var timestamp = DateTimeOffset.ParseExact(match.Groups[1].Value, dateFormat, null);
                        var level = ParseNativeLogLevel(match.Groups[2].Value);
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

            if (currentLine is not null)
            {
                allLogs.Add(currentLine);
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
    }

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

    private (string Arch, string Ext) GetLibDdWafUnixArchitectureAndExtension() =>
        (IsOsx) switch
        {
            (true) => ($"osx", "dylib"), // LibDdWaf is universal binary on osx
            (false) => ($"linux-{UnixArchitectureIdentifier}", "so"),
        };

    private (string Arch, string Ext) GetUnixArchitectureAndExtension() =>
        (IsOsx, IsAlpine) switch
        {
            (true, _) => ($"osx-{UnixArchitectureIdentifier}", "dylib"),
            (false, false) => ($"linux-{UnixArchitectureIdentifier}", "so"),
            (false, true) => ($"linux-musl-{UnixArchitectureIdentifier}", "so"),
        };

    // the integration tests need their own copy of the profiler, this achieved through build.props on Windows, but doesn't seem to work under Linux
    private void IntegrationTestLinuxOrOsxProfilerDirFudge(string project)
    {
        // Not sure if/why this is necessary, and we can't just point to the correct output location
        var src = MonitoringHomeDirectory;
        var testProject = Solution.GetProject(project).Directory;
        var dest = testProject / "bin" / BuildConfiguration / Framework / "profiler-lib";
        CopyDirectoryRecursively(src, dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);

        // not sure exactly where this is supposed to go, may need to change the original build
        if (IsLinux)
        {
            foreach (var linuxDir in MonitoringHomeDirectory.GlobDirectories("linux-*"))
            {
                CopyDirectoryRecursively(linuxDir, dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
            }
        }
        else if (IsOsx)
        {
            foreach (var osxDir in MonitoringHomeDirectory.GlobDirectories("osx"))
            {
                CopyDirectoryRecursively(osxDir, dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
            }
        }
    }

    private void CopyDumpsToBuildData()
    {
        CopyDumpsTo(BuildDataDirectory);
    }

    private void CopyDumpsTo(AbsolutePath root)
    {
        if (Directory.Exists(TempDirectory))
        {
            foreach (var dump in GlobFiles(TempDirectory, "coredump*", "*.dmp"))
            {
                MoveFileToDirectory(dump, root / "dumps", FileExistsPolicy.Overwrite);
            }
        }

        foreach (var file in Directory.EnumerateFiles(TracerDirectory, "*.dmp", SearchOption.AllDirectories))
        {
            CopyFileToDirectory(file, root, FileExistsPolicy.OverwriteIfNewer);
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

    private void DotnetBuild(
        Project project,
        TargetFramework framework = null,
        bool noRestore = true,
        bool noDependencies = true
        )
    {
        DotnetBuild(new [] { project.Path }, framework, noRestore, noDependencies);
    }

    private void DotnetBuild(
        IEnumerable<AbsolutePath> projPaths,
        TargetFramework framework = null,
        bool noRestore = true,
        bool noDependencies = true)
    {
        DotNetBuild(s => s
            .SetConfiguration(BuildConfiguration)
            .SetTargetPlatformAnyCPU()
            .When(noRestore, settings => settings.EnableNoRestore())
            .When(noDependencies, settings => settings.EnableNoDependencies())
            .When(framework is not null, settings => settings.SetFramework(framework))
            .When(DebugType is not null, settings => settings.SetProperty(nameof(DebugType), DebugType))
            .When(Optimize is not null, settings => settings.SetProperty(nameof(Optimize), Optimize))
            .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
            .When(TestAllPackageVersions, o => o.SetProperty("TestAllPackageVersions", "true"))
            .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
            .SetNoWarnDotNetCore3()
            .SetProcessArgumentConfigurator(arg => arg.Add("/nowarn:NU1701")) //nowarn:NU1701 - Package 'x' was restored using '.NETFramework,Version=v4.6.1' instead of the project target framework '.NETCoreApp,Version=v2.1'.
            .CombineWith(projPaths, (settings, projPath) => settings.SetProjectFile(projPath)));
    }
}
