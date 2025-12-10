using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodeGenerators;
using ICSharpCode.SharpZipLib.Zip;
using LogParsing;
using Mono.Cecil;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using Logger = Serilog.Log;

// #pragma warning disable SA1306
// #pragma warning disable SA1134
// #pragma warning disable SA1111
// #pragma warning disable SA1400
// #pragma warning disable SA1401

partial class Build
{
    [Solution("Datadog.Trace.sln")] readonly Solution Solution;
    [Solution("Datadog.Trace.Samples.g.sln")] readonly Solution SamplesSolution;
    AbsolutePath TracerDirectory => RootDirectory / "tracer";
    AbsolutePath SharedDirectory => RootDirectory / "shared";
    AbsolutePath ProfilerDirectory => RootDirectory / "profiler";
    AbsolutePath MsBuildProject => TracerDirectory / "Datadog.Trace.proj";
    AbsolutePath BuildArtifactsDirectory => RootDirectory / "artifacts";

    AbsolutePath OutputDirectory => TracerDirectory / "bin";
    AbsolutePath SymbolsDirectory => OutputDirectory / "symbols";
    AbsolutePath ArtifactsDirectory => Artifacts ?? (OutputDirectory / "artifacts");
    AbsolutePath WindowsTracerHomeZip => ArtifactsDirectory / "windows-tracer-home.zip";
    AbsolutePath WindowsSymbolsZip => ArtifactsDirectory / "windows-native-symbols.zip";
    AbsolutePath OsxTracerHomeZip => ArtifactsDirectory / "macOS-tracer-home.zip";
    AbsolutePath BuildDataDirectory => BuildArtifactsDirectory / "build_data";
    AbsolutePath MsbuildDebugPath => TestLogsDirectory / "msbuild";
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

    const string LibDdwafVersion = "1.30.0";

    string[] OlderLibDdwafVersions = { "1.3.0", "1.10.0", "1.14.0", "1.16.0", "1.23.0" };

    AbsolutePath LibDdwafDirectory(string libDdwafVersion = null) => (NugetPackageDirectory ?? RootDirectory / "packages") / $"libddwaf.{libDdwafVersion ?? LibDdwafVersion}";

    AbsolutePath SourceDirectory => TracerDirectory / "src";
    AbsolutePath BuildDirectory => TracerDirectory / "build";
    AbsolutePath TestsDirectory => TracerDirectory / "test";
    AbsolutePath BundleHomeDirectory => Solution.GetProject(Projects.DatadogTraceBundle).Directory / "home";
    AbsolutePath DatadogTraceDirectory => Solution.GetProject(Projects.DatadogTrace).Directory;
    AbsolutePath BenchmarkHomeDirectory => Solution.GetProject(Projects.DatadogTraceBenchmarkDotNet).Directory / "home";

    readonly TargetFramework[] AppTrimmingTFMs = { TargetFramework.NETCOREAPP3_1, TargetFramework.NET6_0 };

    AbsolutePath SharedTestsDirectory => SharedDirectory / "test";

    AbsolutePath TempDirectory => (AbsolutePath)(IsWin ? Path.GetTempPath() : "/tmp/");

    readonly string[] WindowsArchitectureFolders = { "win-x86", "win-x64" };
    Project NativeTracerProject => Solution.GetProject(Projects.ClrProfilerNative);
    Project NativeTracerTestsProject => Solution.GetProject(Projects.NativeTracerNativeTests);
    Project NativeLoaderProject => Solution.GetProject(Projects.NativeLoader);
    Project NativeLoaderTestsProject => Solution.GetProject(Projects.NativeLoaderNativeTests);

    [LazyPathExecutable(name: "cmake")] readonly Lazy<Tool> CMake;
    [LazyPathExecutable(name: "make")] readonly Lazy<Tool> Make;
    [LazyPathExecutable(name: "tar")] readonly Lazy<Tool> Tar;
    [LazyPathExecutable(name: "nfpm")] readonly Lazy<Tool> Nfpm;
    [LazyPathExecutable(name: "cmd")] readonly Lazy<Tool> Cmd;
    [LazyPathExecutable(name: "chmod")] readonly Lazy<Tool> Chmod;
    [LazyPathExecutable(name: "objcopy")] readonly Lazy<Tool> ExtractDebugInfo;
    [LazyPathExecutable(name: "strip")] readonly Lazy<Tool> StripBinary;
    [LazyPathExecutable(name: "ln")] readonly Lazy<Tool> HardLinkUtil;
    [LazyPathExecutable(name: "cppcheck")] readonly Lazy<Tool> CppCheck;
    [LazyPathExecutable(name: "run-clang-tidy")] readonly Lazy<Tool> RunClangTidy;
    [LazyPathExecutable(name: "patchelf")] readonly Lazy<Tool> PatchElf;
    [LazyPathExecutable(name: "nm")] readonly Lazy<Tool> Nm;

    //OSX Tools
    readonly string[] OsxArchs = { "arm64", "x86_64" };
    [LazyPathExecutable(name: "otool")] readonly Lazy<Tool> OTool;
    [LazyPathExecutable(name: "lipo")] readonly Lazy<Tool> Lipo;

    bool IsGitlab => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_JOB_ID"));

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
        Solution.GetProject(Projects.DatadogTraceManual),
        Solution.GetProject(Projects.DatadogTraceOpenTracing),
        Solution.GetProject(Projects.DatadogTraceAnnotations),
        Solution.GetProject(Projects.DatadogTraceTrimming),
    };

    Project[] ParallelIntegrationTests => new[]
    {
        Solution.GetProject(Projects.TraceIntegrationTests),
    };

    Project[] ClrProfilerIntegrationTests
        => IsOsx
               ? new[] { Solution.GetProject(Projects.ClrProfilerIntegrationTests), Solution.GetProject(Projects.AppSecIntegrationTests), Solution.GetProject(Projects.DdTraceIntegrationTests) }
               : new[] { Solution.GetProject(Projects.ClrProfilerIntegrationTests), Solution.GetProject(Projects.AppSecIntegrationTests), Solution.GetProject(Projects.DdTraceIntegrationTests), Solution.GetProject(Projects.DdDotnetIntegrationTests) };

    TargetFramework[] TestingFrameworks => GetTestingFrameworks(Platform, IsArm64);

    TargetFramework[] GetTestingFrameworks(PlatformFamily platform, bool isArm64 = false) => (platform, isArm64, IncludeAllTestFrameworks || RequiresThoroughTesting()) switch
    {
        // we only support linux-arm64 on .NET 5+, so we run a different subset of the TFMs for ARM64
        (PlatformFamily.Linux, true, true) => new[] { TargetFramework.NET5_0, TargetFramework.NET6_0, TargetFramework.NET7_0, TargetFramework.NET8_0, TargetFramework.NET9_0, TargetFramework.NET10_0, },
        (PlatformFamily.Linux, true, false) => new[] { TargetFramework.NET5_0, TargetFramework.NET6_0, TargetFramework.NET9_0, TargetFramework.NET10_0, },
        // Don't test 2.1 for now, as the build is broken on master. If/when that's resolved, re-enable
        (PlatformFamily.Windows, _, true) => new[] { TargetFramework.NET48, TargetFramework.NETCOREAPP3_0, TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET6_0, TargetFramework.NET7_0, TargetFramework.NET8_0, TargetFramework.NET9_0, TargetFramework.NET10_0, },
        (PlatformFamily.Windows, _, false) => new[] { TargetFramework.NET48, TargetFramework.NETCOREAPP3_1, TargetFramework.NET9_0, TargetFramework.NET10_0, },
        // Everything else e.g. MaxOS, linux-x64 etc
        // Same as Windows just without the .NET FX
        (_, _, true) => new[] { TargetFramework.NETCOREAPP3_0, TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET6_0, TargetFramework.NET7_0, TargetFramework.NET8_0, TargetFramework.NET9_0, TargetFramework.NET10_0, },
        (_, _, false) => new[] { TargetFramework.NETCOREAPP3_1, TargetFramework.NET9_0, TargetFramework.NET10_0, },
    };

    string ReleaseBranchForCurrentVersion() => new Version(Version).Major switch
    {
        LatestMajorVersion => "origin/master",
        var major => $"origin/release/{major}.x",
    };

    bool RequiresThoroughTesting()
    {
        if (IsLocalBuild)
        {
            // we should always run all tests locally
            return true;
        }

        var baseBranch = string.IsNullOrEmpty(TargetBranch) ? ReleaseBranchForCurrentVersion() : $"origin/{TargetBranch}";
        if (IsGitBaseBranch(baseBranch))
        {
            // do a full run on the main branch
            return true;
        }

        var gitChangedFiles = GetGitChangedFiles(baseBranch);
        var integrationChangedFiles = TargetFrameworks
            .SelectMany(tfm => new[]
            {
                // Changes to the definitions (new integrations etc)
                $"tracer/src/Datadog.Trace/Generated/{tfm}/Datadog.Trace.SourceGenerators/Datadog.Trace.SourceGenerators.InstrumentationDefinitions.InstrumentationDefinitionsGenerator",
                $"tracer/src/Datadog.Trace/Generated/{tfm}/Datadog.Trace.SourceGenerators/AspectsDefinitionsGenerator",
            })
            .Concat(new [] {
                // Changes to the integrations themselves, e.g. change in behaviour
                "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation",
                "tracer/src/Datadog.Trace/Iast/Aspects"
            })
            .ToList();

        var hasIntegrationChanges = gitChangedFiles.Any(s => integrationChangedFiles.Any(s.Contains));
        var snapshotChangeCount = gitChangedFiles.Count(s => s.EndsWith("verified.txt"));

        // If the integrations have changed, we should play it safe and test all the frameworks
        // If a lot of snapshots have changed, we should play it safe
        return hasIntegrationChanges || (snapshotChangeCount > 100);
    }

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
            EnsureExistingDirectory(BuildArtifactsDirectory / "publish");
        });

    Target Restore => _ => _
        .After(Clean)
        .Unlisted()
        .Executes(() =>
        {
            if (FastDevLoop)
            {
                return;
            }

            if (IsWin)
            {
                NuGetTasks.NuGetRestore(s => s
                    .SetTargetPath(Solution)
                    .SetVerbosity(NuGetVerbosity.Normal)
                    .SetProcessLogOutput(!IsServerBuild)
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                        o.SetPackagesDirectory(NugetPackageDirectory)));
            }
            else
            {
                DotNetRestore(s => s
                    .SetProjectFile(Solution)
                    .SetVerbosity(DotNetVerbosity.Minimal)
                    .SetProperty("configuration", BuildConfiguration.ToString())
                    .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                        o.SetPackageDirectory(NugetPackageDirectory)));
            }
        });

    Target CompileTracerNativeSrcWindows => _ => _
        .Unlisted()
        .After(CompileManagedLoader)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // If we're building for x64, build for x86 too
            var platforms = ArchitecturesForPlatformForTracer;

            Logger.Information($"Running in GitLab? {IsGitlab}");

            // Can't use dotnet msbuild, as needs to use the VS version of MSBuild
            // Build native tracer assets
            MSBuild(s => s
                .SetTargetPath(MsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetMSBuildPath()
                .SetTargets("BuildCppSrc")
                .DisableRestore()
                // Gitlab has issues with memory usage...
                .SetMaxCpuCount(IsGitlab ? 1 : null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target CompileTracerNativeSrcLinux => _ => _
        .Unlisted()
        .After(CompileManagedLoader)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");
            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target {FileNames.NativeTracer}");
        });

    Target CompileTracerNativeTestsLinux => _ => _
        .Unlisted()
        .After(CompileManagedLoader)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");
            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target {FileNames.NativeTracerTests}");
        });

    Target CompileNativeSrcMacOs => _ => _
        .Unlisted()
        .After(CompileManagedLoader)
        .OnlyWhenStatic(() => IsOsx)
        .Executes(() =>
        {
            DeleteDirectory(NativeTracerProject.Directory / "build");

            var finalArchs = string.Join(';', OsxArchs);
            var buildDirectory = NativeBuildDirectory + "_" + finalArchs.Replace(';', '_');
            EnsureExistingDirectory(buildDirectory);

            var envVariables = new Dictionary<string, string>
            {
                ["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
                ["CMAKE_OSX_ARCHITECTURES"] = finalArchs,
                ["CMAKE_MAKE_PROGRAM"] = "make",
                ["CMAKE_CXX_COMPILER"] = "clang++",
                ["CMAKE_C_COMPILER"] = "clang",
            };

            // Build native
            CMake.Value(
                arguments: $"-B {buildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}",
                environmentVariables: envVariables);
            CMake.Value(
                arguments: $"--build {buildDirectory} --parallel {Environment.ProcessorCount} --target {FileNames.NativeTracer}",
                environmentVariables: envVariables);

            var sourceFile = NativeTracerProject.Directory / "build" / "bin" / $"{NativeTracerProject.Name}.dylib";

            // Check section with the manager loader
            var output = OTool.Value(arguments: $"-s binary dll {sourceFile}", logOutput: false);
            var outputCount = output.Select(o => o.Type == OutputType.Std).Count();
            if (outputCount < 1000)
            {
                throw new ApplicationException("Managed loader section doesn't have the enough size > 1000");
            }

            foreach (var arch in finalArchs.Split(';'))
            {
                // Check the architecture of the build
                output = Lipo.Value(arguments: $"-archs {sourceFile}", logOutput: false);
                var strOutput = string.Join('\n', output.Where(o => o.Type == OutputType.Std).Select(o => o.Text));
                if (!strOutput.Contains(arch, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ApplicationException($"Invalid architecture, expected: '{arch}', actual: '{strOutput}'");
                }
            }
        });

    Target CompileTracerNativeSrc => _ => _
        .Unlisted()
        .Description("Compiles the native tracer assets")
        .DependsOn(CompileTracerNativeSrcWindows)
        .DependsOn(CompileNativeSrcMacOs)
        .DependsOn(CompileTracerNativeSrcLinux)
        .After(CompileManagedSrc);

    Target CompileTracerNativeTests => _ => _
        .Unlisted()
        .Description("Compiles the native tracer tests assets")
        .DependsOn(CompileTracerNativeTestsWindows)
        .DependsOn(CompileTracerNativeTestsLinux);

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

    Target CompileManagedLoader => _ => _
        .Unlisted()
        .Description("Compiles the managed loader (which is required by the native loader)")
        .After(CreateRequiredDirectories)
        .After(Restore)
        .Executes(() =>
        {
            DotnetBuild(new[] { Solution.GetProject(Projects.ManagedLoader).Path }, noRestore: false, noDependencies: false);
        });

    Target CompileManagedSrc => _ => _
        .Unlisted()
        .Description("Compiles the managed code in the src directory")
        .After(CreateRequiredDirectories)
        .After(Restore)
        .After(CompileManagedLoader)
        .Executes(() =>
        {
            // Removes previously generated files
            // This is mostly to avoid forgetting to remove deprecated files (class name change or path change)
            if (IsWin)
            {
                EnsureCleanDirectory(DatadogTraceDirectory / "Generated");
            }

            var include = TracerDirectory.GlobFiles(
                "src/**/*.csproj"
            );

            var exclude = TracerDirectory.GlobFiles(
                "src/Datadog.Trace.Bundle/Datadog.Trace.Bundle.csproj",     // no code, used to generate nuget package
                "src/Datadog.AzureFunctions/Datadog.AzureFunctions.csproj", // no code, used to generate nuget package
                "src/Datadog.Trace.Tools.Runner/*.csproj",
                "src/**/Datadog.InstrumentedAssembly*.csproj",
                "src/Datadog.AutoInstrumentation.Generator/*.csproj",
                $"src/{Projects.ManagedLoader}/*.csproj"
            );

            var toBuild = include.Except(exclude);

            DotnetBuild(toBuild, noDependencies: false);

            var nativeGeneratedFilesOutputPath = NativeTracerProject.Directory / "Generated";
            CallSitesGenerator.GenerateCallSites(TargetFrameworks, tfm => DatadogTraceDirectory / "bin" / BuildConfiguration / tfm / Projects.DatadogTrace + ".dll", nativeGeneratedFilesOutputPath);
            CallTargetsGenerator.GenerateCallTargets(TargetFrameworks, tfm => DatadogTraceDirectory / "bin" / BuildConfiguration / tfm / Projects.DatadogTrace + ".dll", nativeGeneratedFilesOutputPath, Version, BuildDirectory);
        });

    Target CompileTracerNativeTestsWindows => _ => _
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

    Target DownloadLibDdwaf => _ => _.Unlisted().After(CreateRequiredDirectories).Executes(() => DownloadWafVersion());

    async Task DownloadWafVersion(string libddwafVersion = null, string uncompressFolderTarget = null)
    {
        var libDdwafUri = new Uri(
            $"https://www.nuget.org/api/v2/package/libddwaf/{libddwafVersion ?? LibDdwafVersion}"
        );
        var libDdwafZip = TempDirectory / "libddwaf.zip";

        using var client = new HttpClient();

        const int maxRetries = 5;
        const int timeoutSeconds = 15;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                Console.WriteLine($"Attempt {attempt}: Downloading {libDdwafUri}...");
                var response = await client.GetAsync(libDdwafUri, cts.Token);
                response.EnsureSuccessStatusCode();

                await using var file = File.Create(libDdwafZip);
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                await stream.CopyToAsync(file, cts.Token);

                Console.WriteLine($"Download succeeded on attempt {attempt}.");
                break; // Exit loop on success
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                Console.WriteLine($"Attempt {attempt} timed out after {timeoutSeconds} seconds.");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");
            }

            if (attempt == maxRetries)
            {
                throw new Exception($"Failed to download {libDdwafUri} after {maxRetries} attempts.");
            }

            // Exponential backoff before retrying
            await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
        }

        uncompressFolderTarget ??= LibDdwafDirectory(libddwafVersion);
        Console.WriteLine($"{libDdwafZip} downloaded. Extracting to {uncompressFolderTarget}...");

        UncompressZipQuiet(libDdwafZip, uncompressFolderTarget);
    }

    Target CopyLibDdwaf => _ => _
        .Unlisted()
        .After(Clean)
        .After(DownloadLibDdwaf)
        .Executes(() =>
        {
            if (IsWin)
            {
                foreach (var architecture in WindowsArchitectureFolders)
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

    Target DownloadLibDatadog => _ => _
                .Unlisted()
                .After(CreateRequiredDirectories)
                .Executes(async () =>
                {
                    if (IsLinux || IsOsx)
                    {
                        if (!Directory.Exists(NativeBuildDirectory))
                        {
                            CMake.Value(
                                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory}");
                        }
                        // In CI, the folder might already exist. Which means that CMake was already configured
                        // and libdatadog artifact (correct one) was already downloaded.
                        // So just reuse it.

                    }
                    else if (IsWin)
                    {
                        var vcpkgExePath = await GetVcpkg();
                        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT") ?? Directory.GetParent(vcpkgExePath).FullName;
                        var vcpkg = ToolResolver.GetLocalTool(vcpkgExePath);

                        foreach (var arch in new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 })
                        {
                            var triplet = $"{arch}-windows";
                            // note that the following are all quoted when entered into the vcpkg call itself
                            // this is necessary to support cases where we have a space in the folder name
                            var installRoot = $@"{BuildArtifactsDirectory}\deps\vcpkg\{triplet}";
                            var downloads = $@"{BuildArtifactsDirectory}\obj\vcpkg\downloads";
                            var packages = $@"{BuildArtifactsDirectory}\obj\vcpkg\packages";
                            var buildTrees = $@"{BuildArtifactsDirectory}\obj\vcpkg\buildtrees";

                            // This big line is the same generated by VS when installing libdatadog while building the profiler
                            vcpkg($@"install --x-wait-for-lock --triplet ""{triplet}"" --vcpkg-root ""{vcpkgRoot}"" ""--x-manifest-root={RootDirectory}"" ""--x-install-root={installRoot}"" --downloads-root ""{downloads}"" --x-packages-root ""{packages}"" --x-buildtrees-root ""{buildTrees}"" --clean-after-build");
                        }
                    }
                });

    Target CopyLibDatadog => _ => _
                .Unlisted()
                .After(DownloadLibDatadog)
                .Executes(() =>
                {
                    if (IsWin)
                    {
                        var vcpkgDepsFolder = BuildArtifactsDirectory / "deps" / "vcpkg";
                        foreach (var architecture in new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 })
                        {
                            var source = vcpkgDepsFolder / $"{architecture}-windows" / $"{architecture}-windows";
                            if (BuildConfiguration == Configuration.Debug)
                            {
                                source /= "debug";
                            }

                            var dllFile = source / "bin" / "datadog_profiling_ffi.dll";
                            var dest = MonitoringHomeDirectory / $"win-{architecture}";
                            CopyFileToDirectory(dllFile, dest, FileExistsPolicy.Overwrite);

                            var pdbFile = source / "bin" / "datadog_profiling_ffi.pdb";
                            dest = SymbolsDirectory / $"win-{architecture}";
                            CopyFileToDirectory(pdbFile, dest, FileExistsPolicy.Overwrite);
                        }
                    }
                    else if (IsLinux)
                    {
                        var (destArch, ext) = GetUnixArchitectureAndExtension();

                        var libdatadogFileName = $"libdatadog_profiling.{ext}";

                        var source = NativeBuildDirectory / "libdatadog-install" / "lib" / libdatadogFileName;
                        var dest = MonitoringHomeDirectory / destArch;
                        CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
                    }
                    else if (IsOsx)
                    {
                        var libdatadogFileName = $"libdatadog_profiling.dylib";

                        var source = NativeBuildDirectory / "libdatadog-install" / "lib" / libdatadogFileName;
                        var dest = MonitoringHomeDirectory / "osx";
                        CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);
                    }
                });

    Target CopyNativeFilesForTests => _ => _
        .Unlisted()
        .After(Clean, BuildTracerHome)
        .Before(RunIntegrationTests, RunManagedUnitTests)
        .Executes(() =>
        {
            // Copy the native files to all the test projects for simplicity.
            var testProjects = Solution.GetProjects("*Tests")
                                       .Where(p => p.SolutionFolder.Name == "test"
                                               &&  p.Path.ToString().EndsWith(".csproj")); // exclude native test projects
            foreach(var projectName in testProjects)
            {
                Logger.Information("Copying native files for project {ProjectName}", projectName);
                var project = Solution.GetProject(projectName);
                var testDir = project!.Directory;
                var frameworks = project.GetTargetFrameworks();

                if (Framework is not null)
                {
                    frameworks = frameworks.Where(x=> x == Framework).ToList();
                }

                var testBinFolder = testDir / "bin" / BuildConfiguration;

                var (ext, source, libdatadog) = Platform switch
                {
                    PlatformFamily.Windows => ("dll", MonitoringHomeDirectory / $"win-{TargetPlatform}", "datadog_profiling_ffi.dll"),
                    PlatformFamily.Linux => ("so", MonitoringHomeDirectory / GetUnixArchitectureAndExtension().Arch, "libdatadog_profiling.so"),
                    PlatformFamily.OSX => ("dylib", MonitoringHomeDirectory / "osx", "libdatadog_profiling.dylib"),
                    _ => throw new NotSupportedException($"Unsupported platform: {Platform}")
                };

                var libs = new[]
                {
                    (libdatadog, $"LibDatadog.{ext}"),
                    ($"Datadog.Tracer.Native.{ext}", $"Datadog.Tracer.Native.{ext}"),
                };

                foreach (var framework in frameworks)
                {
                    foreach (var lib in libs)
                    {
                        var dest = testBinFolder / framework / lib.Item2;
                        CopyFile(source / lib.Item1, dest, FileExistsPolicy.Overwrite);
                    }
                }
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

                    // dotnet test runs under x86 for net461, even on x64 platforms
                    // so copy both, just to be safe
                    if (IsWin)
                    {
                        foreach (var olderLibDdwafVersion in OlderLibDdwafVersions)
                        {
                            var oldVersionTempPath = TempDirectory / $"libddwaf.{olderLibDdwafVersion}";
                            await DownloadWafVersion(olderLibDdwafVersion, oldVersionTempPath);
                            foreach (var arch in WindowsArchitectureFolders)
                            {
                                var oldVersionPath = oldVersionTempPath / "runtimes" / arch / "native" / "ddwaf.dll";
                                var source = MonitoringHomeDirectory / arch;
                                foreach (var framework in frameworks)
                                {
                                    var dest = testBinFolder / framework / arch;
                                    CopyDirectoryRecursively(source, dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
                                    CopyFile(oldVersionPath, dest / $"ddwaf-{olderLibDdwafVersion}.dll", FileExistsPolicy.Overwrite);
                                }
                            }
                        }
                    }
                    else
                    {
                        var (arch, _) = GetUnixArchitectureAndExtension();
                        var (archWaf, ext) = GetLibDdWafUnixArchitectureAndExtension();

                        foreach (var olderLibDdwafVersion in OlderLibDdwafVersions)
                        {
                            var patchedArchWaf = (IsOsx && olderLibDdwafVersion == "1.3.0") ? archWaf + "-x64" : archWaf;
                            var oldVersionTempPath = TempDirectory / $"libddwaf.{olderLibDdwafVersion}";
                            var oldVersionPath = oldVersionTempPath / "runtimes" / patchedArchWaf / "native" / $"libddwaf.{ext}";
                            await DownloadWafVersion(olderLibDdwafVersion, oldVersionTempPath);
                            {
                                foreach (var framework in frameworks)
                                {
                                    // We have to copy into the _root_ test bin folder here, not the arch sub-folder.
                                    // This is because these tests try to load the WAF.
                                    // Loading the WAF requires using the native tracer as a proxy, which means either
                                    // - The native tracer must be loaded first, so it can rewrite the PInvoke calls
                                    // - The native tracer must be side-by-side with the running dll
                                    // As this is a managed-only unit test, the native tracer _must_ be in the root folder
                                    // For simplicity, we just copy all the native dlls there
                                    var dest = testBinFolder / framework;

                                    // use the files from the monitoring native folder
                                    CopyDirectoryRecursively(MonitoringHomeDirectory / (IsOsx ? "osx" : arch), dest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
                                    CopyFile(oldVersionPath, dest / $"libddwaf-{olderLibDdwafVersion}.{ext}", FileExistsPolicy.Overwrite);
                                }
                            }
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

            try
            {
                // Publish Datadog.Trace.MSBuild which includes Datadog.Trace
                DotNetPublish(s => s
                    .SetProject(Solution.GetProject(Projects.DatadogTraceMsBuild))
                    .SetConfiguration(BuildConfiguration)
                    .SetTargetPlatformAnyCPU()
                    .EnableNoBuild()
                    .EnableNoRestore()
                    .CombineWith(targetFrameworks, (p, framework) => p
                        .SetFramework(framework)
                        .SetOutput(MonitoringHomeDirectory / framework)
                    .SetProcessEnvironmentVariable("MSBUILDDEBUGPATH", MsbuildDebugPath))
                );
            }
            catch
            {
                // Print tail of MSBuild failure notes, if any, then rethrow
                MSBuildLogHelper.DumpMsBuildChildFailures(MsbuildDebugPath);
                throw;
            }
        });

    Target PublishManagedTracerR2R => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .Executes(() =>
        {
            var targetFramework = TargetFramework.NET6_0;

            // Needed as we need to restore with the RuntimeIdentifier
            DotNetRestore(s => s
                .SetProjectFile(Solution.GetProject(Projects.DatadogTraceMsBuild))
                .SetPublishReadyToRun(true)
                .SetRuntime(RuntimeIdentifier)
            );

            DotNetPublish(s => s
                .SetProject(Solution.GetProject(Projects.DatadogTraceMsBuild))
                .SetConfiguration(BuildConfiguration)
                .SetTargetPlatformAnyCPU()
                .SetPublishReadyToRun(true)
                .SetRuntime(RuntimeIdentifier)
                .SetSelfContained(false)
                .SetFramework(targetFramework)
                .SetOutput(MonitoringHomeDirectory / targetFramework)
            );
        });

    Target PublishNativeSymbolsWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileTracerNativeSrc, PublishManagedTracer)
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

    Target PublishDdDotnetSymbolsWindows => _ => _
      .Unlisted()
      .OnlyWhenStatic(() => IsWin)
      .After(BuildDdDotnet, PublishManagedTracer)
      .Executes(() =>
      {
          var source = ArtifactsDirectory / "dd-dotnet" / "win-x64" / "dd-dotnet.pdb";
          var dest = SymbolsDirectory / "dd-dotnet-win-x64" / "dd-dotnet.pdb";
          CopyFile(source, dest, FileExistsPolicy.Overwrite);
      });

    Target PublishNativeTracerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileTracerNativeSrc, PublishManagedTracer)
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
        .After(CompileTracerNativeSrc, PublishManagedTracer)
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
        .After(CompileTracerNativeSrc, PublishManagedTracer)
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

    Target BuildFleetInstaller => _ => _
        .Unlisted()
        .Description("Builds and publishes the fleet installer binary files")
        .After(Clean, Restore, CompileManagedSrc)
        .Before(SignDlls)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // Build the fleet installer project
            var project = SourceDirectory / "Datadog.FleetInstaller" / "Datadog.FleetInstaller.csproj";
            var tfms = Solution.GetProject(project).GetTargetFrameworks();
            // we should only have a single tfm for fleet installer
            if (tfms.Count != 1)
            {
                throw new ApplicationException("Fleet installer should only have a single target framework but found: " + string.Join(", ", tfms));
            }

            var publishFolder = ArtifactsDirectory / "Datadog.FleetInstaller";
            DotNetPublish(s => s
                              .SetProject(project)
                              .SetConfiguration(BuildConfiguration)
                              .SetOutput(publishFolder)
                              .CombineWith(tfms, (p, tfm) => p.SetFramework(tfm)));
        });

    Target PublishFleetInstaller => _ => _
        .Unlisted()
        .Description("Publishes the fleet installer binary files as a zip")
        .DependsOn(BuildFleetInstaller)
        .After(SignDlls)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            var publishFolder = ArtifactsDirectory / "Datadog.FleetInstaller";
            CompressZip(publishFolder, ArtifactsDirectory / "fleet-installer.zip", fileMode: FileMode.Create);
        });

    Target BuildMsi => _ => _
        .Unlisted()
        .Description("Builds the .msi files from the repo")
        .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader, BuildDdDotnet)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // We don't produce an x86-only MSI any more
            var architectures = ArchitecturesForPlatformForTracer.Where(x => x != MSBuildTargetPlatform.x86);

            MSBuild(s => s
                    .SetTargetPath(SharedDirectory / "src" / "msi-installer" / "WindowsInstaller.wixproj")
                    .SetConfiguration(BuildConfiguration)
                    .SetMSBuildPath()
                    .AddProperty("RunWixToolsOutOfProc", true)
                    .SetProperty("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetMaxCpuCount(null)
                    .CombineWith(architectures, (o, arch) => o
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

            // Add the dd-dotnet scripts
            CopyFileToDirectory(BuildDirectory / "artifacts" / "dd-dotnet.cmd", BundleHomeDirectory, FileExistsPolicy.Overwrite);
            CopyFileToDirectory(BuildDirectory / "artifacts" / "dd-dotnet.sh", BundleHomeDirectory, FileExistsPolicy.Overwrite);
        });

    Target CreateBenchmarkIntegrationHome => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .After(BuildProfilerHome)
        .After(BuildNativeLoader)
        .Executes(() =>
        {
            // clean directory of everything except the text files
            BenchmarkHomeDirectory
               .GlobFiles("*.*")
               .Where(filepath => Path.GetExtension(filepath) != ".txt")
               .ForEach(DeleteFile);
            // Copy existing files from tracer home to the Benchmark location
            var requiredFiles = new[]
            {
                "Datadog.Profiler.Native.dll",
                "Datadog.Trace.ClrProfiler.Native.dll",
                "Datadog.Profiler.Native.so",
                "Datadog.Trace.ClrProfiler.Native.so",
                "loader.conf",
            };
            CopyDirectoryRecursively(MonitoringHomeDirectory, BenchmarkHomeDirectory, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite, excludeFile: info =>
            {
                return Array.FindIndex(requiredFiles, s => s == info.Name) == -1;
            });
        });

    Target ExtractDebugInfoLinux => _ => _
        .Unlisted()
        .After(BuildProfilerHome, BuildTracerHome, BuildNativeLoader, BuildNativeWrapper)
        .Executes(() =>
        {
            // extract debug info from everything in monitoring home and copy it to the linux symbols directory
            // except libdatadog since debug symbols are already stripped as part of libdatadog release.
            var files = MonitoringHomeDirectory.GlobFiles("linux-*/*.so")
                .Where(file => !Path.GetFileName(file).Contains("libdatadog_profiling.so"));

            foreach (var file in files)
            {
                var outputDir = SymbolsDirectory / new FileInfo(file).Directory!.Name;
                EnsureExistingDirectory(outputDir);
                var outputFile = outputDir / Path.GetFileNameWithoutExtension(file);
                var debugOutputFile = outputFile + ".debug";

                Logger.Information($"Extracting debug symbol for {file} to {outputFile}.debug");
                ExtractDebugInfo.Value(arguments: $"--only-keep-debug {file} {debugOutputFile}");

                Logger.Information($"Stripping out unneeded information from {file}");
                StripBinary.Value(arguments: $"--strip-unneeded {file}");

                Logger.Information($"Add .gnu_debuglink for {file} targeting {debugOutputFile}");
                ExtractDebugInfo.Value(arguments: $"--add-gnu-debuglink={debugOutputFile} {file}");
            }
        });

    Target CopyDdDotnet => _ => _
        .After(BuildDdDotnet)
        .Executes(() =>
        {
            var script = IsWin ? "dd-dotnet.cmd" : "dd-dotnet.sh";
            CopyFileToDirectory(BuildDirectory / "artifacts" / script, MonitoringHomeDirectory, FileExistsPolicy.Overwrite);

            if (IsLinux)
            {
                Chmod.Value.Invoke("+x " + MonitoringHomeDirectory / script);
            }
        });

    Target ZipSymbols => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .DependsOn(PublishNativeSymbolsWindows)
        .DependsOn(PublishDdDotnetSymbolsWindows)
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
        .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader, SignDlls)
        .DependsOn(CopyDdDotnet)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            CompressZip(MonitoringHomeDirectory, WindowsTracerHomeZip, fileMode: FileMode.Create);
        });

    Target ZipMonitoringHomeLinux => _ => _
        .Unlisted()
        .After(BuildTracerHome, BuildManagedTracerHome, BuildNativeTracerHome, BuildProfilerHome, BuildNativeLoader)
        .DependsOn(CopyDdDotnet)
        .OnlyWhenStatic(() => IsLinux)
        .Requires(() => Version)
        .Executes(() =>
        {
            var tar = Tar.Value;
            var nfpm = Nfpm.Value;

            var (arch, ext) = GetUnixArchitectureAndExtension();
            var workingDirectory = ArtifactsDirectory / $"linux-{UnixArchitectureIdentifier}";
            EnsureCleanDirectory(workingDirectory);

            const string packageName = "datadog-dotnet-apm";

            // debian does not have a specific field for the license, instead you pack the license file in a specific location
            // (See https://github.com/goreleaser/nfpm/issues/847 for discussion)
            var debLicesnse =
                $"""
                 Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
                 Source: https://github.com/DataDog/dd-trace-dotnet
                 Upstream-Name: {packageName}

                 Files:
                  *
                 Copyright: 2017 Datadog, Inc <package@datadoghq.com>
                 License: Apache-2.0
                  Licensed under the Apache License, Version 2.0 (the "License"); you may not
                  use this file except in compliance with the License. You may obtain a copy of
                  the License at
                  .
                     http://www.apache.org/licenses/LICENSE-2.0
                  .
                  Unless required by applicable law or agreed to in writing, software
                  distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
                  WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
                  License for the specific language governing permissions and limitations under
                  the License.
                 Comment:
                  On Debian-based systems the full text of the Apache version 2.0 license can be
                  found in `/usr/share/common-licenses/Apache-2.0'.
                 """;
            var debLicensePath = TempDirectory / "deb-license";
            File.WriteAllText(debLicensePath, debLicesnse);

            foreach (var packageType in LinuxPackageTypes)
            {
                Logger.Information("Creating '{PackageType}' package", packageType);
                var assetsDirectory = TemporaryDirectory / arch / packageType;
                var isTar = packageType == "tar";
                var muslArch = GetUnixArchitectureAndExtension(isOsx: false, isAlpine: true).Arch;

                if (isTar)
                {
                    var includeMuslArtifacts = !IsAlpine;

                    // On x64, for tar only, we package the linux-musl-x64 target as well, to simplify onboarding
                    PrepareMonitoringHomeLinuxForPackaging(assetsDirectory, arch, ext, muslArch, includeMuslArtifacts);

                    // technically we don't need these scripts, but we've been including them in the tar, so keep doing that
                    var scriptsDir = assetsDirectory / ".scripts";
                    EnsureExistingDirectory(scriptsDir);
                    CopyFile(BuildDirectory / "artifacts" / FileNames.AfterInstallScript, scriptsDir / "after_install");
                    CopyFile(BuildDirectory / "artifacts" / FileNames.AfterRemoveScript, scriptsDir / "after_remove");

                    var tarOutputPath = (IsAlpine, RuntimeInformation.ProcessArchitecture) switch
                    {
                        (true, Architecture.X64) => workingDirectory / $"{packageName}-{Version}-musl.tar.gz",
                        (true, var a) => workingDirectory / $"{packageName}-{Version}-musl.{a.ToString().ToLower()}.tar.gz",
                        (false, Architecture.X64) => workingDirectory / $"{packageName}-{Version}.tar.gz",
                        (false, var a) => workingDirectory / $"{packageName}-{Version}.{a.ToString().ToLower()}.tar.gz",
                    };

                    tar($"-czvf {tarOutputPath} .", workingDirectory: assetsDirectory);
                }
                else
                {
                    PrepareMonitoringHomeLinuxForPackaging(assetsDirectory, arch, ext, muslArch, includeMuslArtifacts: false);
                    var nFpmArch = RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => "amd64",
                        Architecture.Arm64 => "arm64",
                        _ => throw new NotSupportedException($"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}")
                    };

                    // language=yaml
                    var yaml =
                        $"""
                         name: "{packageName}"
                         arch: "{nFpmArch}"
                         platform: "linux"
                         version: "{Version}"
                         maintainer: "Datadog Packages <package@datadoghq.com>"
                         description: "Datadog APM client library for .NET"
                         vendor: "Datadog <package@datadoghq.com>"
                         homepage: "https://github.com/DataDog/dd-trace-dotnet"
                         # We were previously using "Apache License 2.0" but that's not technically correct
                         # As needs to be one of the standard fedora licences here: https://docs.fedoraproject.org/en-US/legal/allowed-licenses/
                         # and is not used directly by the deb package format: https://www.debian.org/doc/debian-policy/ch-docs.html
                         license: "Apache-2.0"
                         priority: extra
                         section: default
                         scripts:
                           postinstall: {BuildDirectory / "artifacts" / FileNames.AfterInstallScript}
                           postremove: {BuildDirectory / "artifacts" / FileNames.AfterRemoveScript}
                         rpm:
                             # The package group. This option is deprecated by most distros
                             # but we added it with fpm, so keeping it here for consistency
                             group: default
                             prefixes:
                             - /opt/datadog
                         contents:
                         - src: {assetsDirectory}/
                           dst: /opt/datadog
                           type: tree
                         - src: {debLicensePath}
                           dst: /usr/share/doc/{packageName}/copyright
                           packager: deb
                           file_info:
                             mode: 0644
                         """;

                    var npfmConfig = TempDirectory / "nfpm.yaml";
                    // overwrites if it exists
                    File.WriteAllText(npfmConfig, yaml);
                    nfpm($"package -f {npfmConfig} -p {packageType}", workingDirectory: workingDirectory);
                }
            }

            return;

            void PrepareMonitoringHomeLinuxForPackaging(AbsolutePath assetsDirectory, string arch, string ext, string muslArch, bool includeMuslArtifacts)
            {
                var chmod = Chmod.Value;

                // On x64 we package the linux-musl-x64 target as well, to simplify onboarding,
                // but we don't need this on arm64 (currently) or on deb/rpm artifacts
                // (as those aren't installable on alpine)
                EnsureCleanDirectory(assetsDirectory);
                CopyDirectoryRecursively(MonitoringHomeDirectory, assetsDirectory, DirectoryExistsPolicy.Merge);

                // remove the XML files and pdb files from the package - they take up space and aren't needed
                assetsDirectory.GlobFiles("**/*.xml", "**/*.pdb").ForEach(DeleteFile);

                if (!includeMuslArtifacts && !IsAlpine)
                {
                    // Remove the linux-musl-x64 folder entirely if we don't need it
                    Logger.Information("Removing musl assets as not required");
                    DeleteDirectory(assetsDirectory / muslArch);
                }

                // For back-compat reasons, we must always have the Datadog.ClrProfiler.Native.so file in the root folder
                // as it's set in the COR_PROFILER_PATH etc env var
                // so create a symlink to avoid bloating package sizes
                var archSpecificFile = assetsDirectory / arch / $"{FileNames.NativeLoader}.{ext}";
                var linkLocation = assetsDirectory / $"{FileNames.NativeLoader}.{ext}";
                HardLinkUtil.Value($"-v {archSpecificFile} {linkLocation}");

                if (includeMuslArtifacts)
                {
                    // The native loader file is the same for glibc/musl so can share the file
                    var muslLinkLocation = assetsDirectory / muslArch / $"{FileNames.NativeLoader}.{ext}";
                    DeleteFile(muslLinkLocation); // remove the original file and replace it with a link
                    HardLinkUtil.Value($"-v {archSpecificFile} {muslLinkLocation}");
                }

                // For back-compat reasons, we have to keep the libddwaf.so file in the root folder
                // because the way AppSec probes the paths won't find the linux-musl-x64 target currently
                archSpecificFile = assetsDirectory / arch / FileNames.AppSecLinuxWaf;
                linkLocation = assetsDirectory / FileNames.AppSecLinuxWaf;
                HardLinkUtil.Value($"-v {archSpecificFile} {linkLocation}");

                if (includeMuslArtifacts)
                {
                    // The WAF file is the same for glibc/musl so can share the file
                    var muslLinkLocation = assetsDirectory / muslArch / FileNames.AppSecLinuxWaf;
                    DeleteFile(muslLinkLocation);
                    HardLinkUtil.Value($"-v {archSpecificFile} {muslLinkLocation}");
                }

                // we must always have the Datadog.Linux.ApiWrapper.<arch>.so file in the continuousprofiler subfolder
                // as it's set in the LD_PRELOAD env var
                var continuousProfilerDir = assetsDirectory / "continuousprofiler";
                EnsureExistingDirectory(continuousProfilerDir);
                var wrapperFileName = FileNames.GetProfilerLinuxApiWrapper(arch);
                archSpecificFile = assetsDirectory / arch / wrapperFileName;
                linkLocation = continuousProfilerDir / wrapperFileName;
                HardLinkUtil.Value($"-v {archSpecificFile} {linkLocation}");

                if (includeMuslArtifacts)
                {
                    // The wrapper library is the same for glibc/musl so can share the file
                    var muslWrapperFileName = FileNames.GetProfilerLinuxApiWrapper(muslArch);
                    var muslLinkLocation = assetsDirectory / muslArch / muslWrapperFileName;
                    DeleteFile(muslLinkLocation);
                    HardLinkUtil.Value($"-v {archSpecificFile} {muslLinkLocation}");
                }

                // Need to copy in the loader.conf file into the musl arch folder
                if (includeMuslArtifacts)
                {
                    // The files are identical in the glibc and musl folders, as they point to
                    // relative files, so we can just hardlink them
                    archSpecificFile = assetsDirectory / arch / FileNames.LoaderConf;
                    var muslLinkLocation = assetsDirectory / muslArch / FileNames.LoaderConf;
                    DeleteFile(muslLinkLocation); // probably won't exist, but be safe
                    // copy the loader.conf into the musl arch folder
                    HardLinkUtil.Value($"-v {archSpecificFile} {muslLinkLocation}");
                }

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
                    replacement: $@";$1;./$1/Datadog.");
                File.WriteAllText(assetsDirectory / FileNames.LoaderConf, contents: loaderConfContents);

                // Copy createLogPath.sh script and set the permissions
                CopyFileToDirectory(BuildDirectory / "artifacts" / FileNames.CreateLogPathScript, assetsDirectory);
                chmod.Invoke($"+x {assetsDirectory / FileNames.CreateLogPathScript}");
            }
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
        .DependsOn(CopyNativeFilesForTests)
        .DependsOn(CompileManagedTestHelpers)
        .DependsOn(CompileManagedLoader)
        .Executes(() =>
        {
            DotnetBuild(TracerDirectory.GlobFiles("test/**/*.Tests.csproj"));
        });

    Target RunManagedUnitTests => _ => _
        .Unlisted()
        .After(CompileManagedUnitTests)
        .DependsOn(CleanTestLogs)
        .Executes(() =>
        {
            var testProjects = TracerDirectory.GlobFiles("test/**/*.Tests.csproj")
                .Select(x => Solution.GetProject(x))
                .ToList();

            testProjects.ForEach(EnsureResultsDirectory);
            var filter = string.IsNullOrWhiteSpace(Filter) && IsArm64 ? "(Category!=ArmUnsupported)&(Category!=AzureFunctions)&(SkipInCI!=True)" : Filter;
            var exceptions = new List<Exception>();
            try
            {
                foreach (var targetFramework in TestingFrameworks.Where(x => x == Framework || Framework is null))
                {
                    if (IsArm64 && Framework is null && targetFramework == TargetFramework.NETCOREAPP2_1)
                    {
                        // Skip .NET Core 2.1 on ARM64 unless enabled explicitly - Some unit tests crash and it's not supported anyway
                        continue;
                    }

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
                            .When(CodeCoverageEnabled, ConfigureCodeCoverage)
                            .When(!string.IsNullOrWhiteSpace(Filter), c => c.SetFilter(Filter))
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
            foreach (var platform in ArchitecturesForPlatformForTracer)
            {
                var workingDirectory = TestsDirectory / "Datadog.Tracer.Native.Tests" / "bin" / BuildConfiguration.ToString() / platform;
                var exePath = workingDirectory / "Datadog.Tracer.Native.Tests.exe";

                var testsResultFile = BuildDataDirectory / "tests" / $"Datadog.Tracer.Native.Tests.Results.{BuildConfiguration}.{platform}.xml";
                var testExe = ToolResolver.GetLocalTool(exePath);
                testExe($"--gtest_output=xml:{testsResultFile}", workingDirectory: workingDirectory);
            }
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

            var testsResultFile = BuildDataDirectory / "tests" / $"{FileNames.NativeTracerTests}.Results.{BuildConfiguration}.{TargetPlatform}.xml";
            var testExe = ToolResolver.GetLocalTool(exePath);

            testExe($"--gtest_output=xml:{testsResultFile}", workingDirectory: workingDirectory);
        });

    Target RunNativeTests => _ => _
        .Unlisted()
        .DependsOn(RunTracerNativeTests)
        .DependsOn(RunNativeLoaderNativeTests)
        .DependsOn(RunProfilerNativeTests);

    Target RunTracerNativeTests => _ => _
        .Unlisted()
        .DependsOn(RunTracerNativeTestsWindows)
        .DependsOn(RunTracerNativeTestsLinux)
        .After(CompileTracerNativeTests);

    Target RunNativeLoaderNativeTests => _ => _
        .Unlisted()
        .DependsOn(RunNativeLoaderTestsWindows)
        .DependsOn(RunNativeLoaderTestsLinux)
        .After(CompileNativeLoaderNativeTests);

    Target RunProfilerNativeTests => _ => _
        .Unlisted()
        .DependsOn(RunProfilerNativeUnitTestsWindows)
        .DependsOn(RunProfilerNativeUnitTestsLinux)
        .After(CompileProfilerNativeTests);

    Target CompileIntegrationTests => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileManagedTestHelpers)
        .After(PublishIisSamples)
        .After(BuildRunnerTool)
        .Requires(() => Framework)
        .Requires(() => MonitoringHomeDirectory != null)
        .Executes(() =>
        {
            // Compile the dependent samples.
            if (!Framework.ToString().StartsWith("net4"))
            {
                DotnetBuild(Solution.GetProject(Projects.RazorPages), framework: Framework);
            }

            var projects = TracerDirectory
                    .GlobFiles("test/*.IntegrationTests/*.IntegrationTests.csproj")
                    .Where(path => !((string)path).Contains(Projects.DebuggerIntegrationTests))
                    .Where(project => Solution.GetProject(project).GetTargetFrameworks().Contains(Framework))
                ;

            DotnetBuild(projects, framework: Framework);
        });

    Target CompileSamples => _ => _
         .Description("Compiles all the sample projects")
         .Unlisted()
         .After(Clean)
         .DependsOn(HackForMissingMsBuildLocation)
         .Executes(() =>
          {
              if (TestAllPackageVersions)
              {
                  // Explicitly compiles the prerequisites which MSbuild doesn't
                  // (no, I don't know why, I can't seem to convince it to do this automatically)
                  var projects = TracerDirectory.GlobFiles(
                      "test/test-applications/integrations/dependency-libs/**/*.csproj",
                      "test/test-applications/integrations/**/*.vbproj"
                  );

                  DotnetBuild(projects, noDependencies: false, noRestore: false);
                  // these are defined in Datadog.Trace.proj - they only build the projects that have multiple package versions of their NuGet dependencies
                  var targets = new[] { "RestoreSamplesForPackageVersionsOnly", "RestoreAndBuildSamplesForPackageVersionsOnly" };
                  var frameworks = Framework is null || string.IsNullOrEmpty(Framework)
                                       ? TestingFrameworks
                                       : new[] { Framework };

                  foreach (var framework in frameworks)
                  {
                      foreach (var target in targets)
                      {
                          // /nowarn:NU1701 - Package 'x' was restored using '.NETFramework,Version=v4.6.1' instead of the project target framework '.NETCoreApp,Version=v2.1'.
                          // /nowarn:NETSDK1138 - Package 'x' was restored using '.NETFramework,Version=v4.6.1' instead of the project target framework '.NETCoreApp,Version=v2.1'.
                          MSBuild(x => x
                                      .SetTargetPath(MsBuildProject)
                                      .SetTargets(target)
                                      .SetConfiguration(BuildConfiguration)
                                      .SetProperty("TargetFramework", framework.ToString())
                                      .SetProperty("BuildInParallel", "true")
                                      .SetProperty("CheckEolTargetFramework", "false")
                                      .SetProperty("ManuallyCopyCodeCoverageFiles", "false")
                                      .When(!string.IsNullOrEmpty(SampleName), o => o.SetProperty("SampleName", SampleName))
                                      .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetProperty("RestorePackagesPath", NugetPackageDirectory))
                                      .SetProcessArgumentConfigurator(arg => arg.Add("/nowarn:NU1701").Add($"/bl:\"{(BuildDataDirectory / $"build_{DateTime.UtcNow.Ticks}.binlog")}\""))
                                      .When(TestAllPackageVersions, o => o.SetProperty("TestAllPackageVersions", "true"))
                                      .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                          );
                      }
                  }

                  // All done with the multi-api version
                  return;
              }

              // Build the "standalone" samples (i.e. the single-api-version samples)
              var samples = GetSamplesToBuild();
              Logger.Information("Building {SampleName}", samples);

              // If we are building a specific sample, with a specific NuGet version, we can target just that one with MSBuild
              // Windows only at the moment as I couldn't get `dotnet build` to work with the ApiVersion parameter
              // As it needs to be restored first, but when it did it couldn't find the assets file
              if (IsWin && !string.IsNullOrEmpty(ApiVersion) && !string.IsNullOrEmpty(SampleName))
              {
                  Logger.Information("Building sample {SampleName} with ApiVersion {ApiVersion} using MSBuild", SampleName, ApiVersion);

                  MSBuild(x => x.SetTargetPath(samples)
                                .SetTargets("Restore", "Build")
                                .SetConfiguration(BuildConfiguration)
                                .SetProperty("ApiVersion", ApiVersion)
                                .When(Framework is not null, o => o.SetProperty("TargetFramework", Framework.ToString()))
                                .SetProperty("BuildInParallel", "true")
                                .SetProcessArgumentConfigurator(arg => arg.Add("/nowarn:NU1701")));
              }
              else
              {
                  // TODO: set Samples.Trimming as don't build, as we have to explicitly build that on every platform anyway
                  DotNetBuild(config => config.SetConfiguration(BuildConfiguration)
                                              .SetProperty("BuildInParallel", "true")
                                              .SetProcessArgumentConfigurator(arg => arg.Add("/nowarn:NU1701"))
                                              .When(Framework is not null, x => x.SetFramework(Framework))
                                              .SetProjectFile(samples));
              }

              string GetSamplesToBuild()
              {
                  // If a specific sample name was not given, build whole samples solution
                  if (string.IsNullOrWhiteSpace(SampleName))
                  {
                      return SamplesSolution;
                  }

                  // Filter to a single candidate SampleName
                  var candidates =
                      TracerDirectory.GlobFiles("test/test-applications/integrations/**/*.csproj")
                                     .Select(x => Solution.GetProject(x))
                                     .Where(project => project is not null
                                                    && project.Path.ToString().Contains(SampleName, StringComparison.OrdinalIgnoreCase));

                  if (Framework is not null)
                  {
                      // exclude projects that can't be built for this TFM
                      candidates = candidates.Where(project => project.TryGetTargetFrameworks()?.Contains(Framework) ?? true);
                  }

                  var allMatches = candidates.ToList();
                  if (allMatches.Count == 0)
                  {
                      throw new InvalidOperationException($"No sample projects found matching '{SampleName}'." +
                                                          (string.IsNullOrEmpty(Framework) ? $" Does the project support the specified framework '{Framework}'?" : " ") +
                                                          "Alternatively, exclude the SampleName parameter to build all samples instead");
                  }

                  if (allMatches.Count == 1)
                  {
                      return allMatches.First();
                  }

                  // try to find best "exact" match
                  // exact name match
                  var bestMatches = allMatches.Where(x => x.Name.Equals(SampleName, StringComparison.Ordinal)).ToList();
                  if(bestMatches.Count == 1)
                  {
                      return bestMatches.First();
                  }

                  // case insensitive exact name match
                  bestMatches = allMatches.Where(x => x.Name.Equals(SampleName, StringComparison.OrdinalIgnoreCase)).ToList();
                  if(bestMatches.Count == 1)
                  {
                      return bestMatches.First();
                  }

                  // case insensitive path suffix match
                  bestMatches = allMatches.Where(x => x.Path.ToString().EndsWith(SampleName, StringComparison.OrdinalIgnoreCase)).ToList();
                  if(bestMatches.Count == 1)
                  {
                      return bestMatches.First();
                  }

                  // no way to choose between them
                  throw new InvalidOperationException($"Found multiple sample projects matching '{SampleName}'. " +
                                                      string.Join(",", allMatches.Select(x => $"'{x.Name}'")) +
                                                      ". Provide an exact match for the name of of the project, or a path suffix");
              }
          });

    Target CompileTrimmingSamples => _ => _
        .Description("Compiles the trimming samples")
        .After(CompileManagedSrc)
        .Requires(() => Framework)
        .Unlisted()
        .After(Clean)
        .DependsOn(HackForMissingMsBuildLocation)
        .Executes(() =>
        {
            // It's a bit of a hack putting these here, but hopefully it _works_ at least
            CompileSamplesThatDependOnDatadogTrace();

            // This is separated from CompileSamples, because we can only publish for the actual platform we're on,
            var trimmingSamples = new []
            {
                (project: "Samples.Trimming",include: Framework.IsGreaterThanOrEqualTo(TargetFramework.NET6_0), r2r: false),
                (project: "Samples.ManualInstrumentation",include: Framework.IsGreaterThanOrEqualTo(TargetFramework.NETCOREAPP2_1), r2r: true),
            };
            var projectsToPublish = trimmingSamples
                                   .Select(x => (project: Solution.GetProject(x.project), x.include, x.r2r))
                                   .Where(x => (x, x.project.TryGetTargetFrameworks(), x.project.RequiresDockerDependency()) switch
                                    {
                                        ({include: false }, _, _) => false,
                                        _ when !string.IsNullOrWhiteSpace(SampleName) => x.project.Path.ToString().Contains(SampleName, StringComparison.OrdinalIgnoreCase),
                                        (_, _, DockerDependencyType.All) => false, // can't use docker on Windows
                                        (_, { } targets, _) => targets.Contains(Framework),
                                        _ => true,
                                    });
            var rid = (Platform, TargetPlatform.ToString()) switch
            {
                (PlatformFamily.Linux, "x64") => IsAlpine ? "linux-musl-x64" : "linux-x64",
                (PlatformFamily.Linux, "ARM64") => IsAlpine ? "linux-musl-arm64" : "linux-arm64",
                (PlatformFamily.OSX, "x64") => "osx-x64",
                (PlatformFamily.OSX, "ARM64") => "osx-arm64",
                (PlatformFamily.Windows, "ARM64" or "ARM64EC") => "win-arm64",
                (PlatformFamily.Windows, "x64") => "win-x64",
                (PlatformFamily.Windows, "x86") => "win-x86",
                _ => throw new InvalidOperationException($"Unknown platform {Platform} ({RuntimeInformation.ProcessArchitecture})"),
            };

            DotNetPublish(config => config
                .SetConfiguration(BuildConfiguration)
                .SetRuntime(rid)
                .SetFramework(Framework)
                .CombineWith(projectsToPublish,
                    (s, project) => s
                        .SetProject(project.project)
                        .When(project.r2r, x => x.SetPublishReadyToRun(true))));

            void CompileSamplesThatDependOnDatadogTrace()
            {
                // These are either directly used by the integration tests or depend on Datadog.Trace already being built
                // It would be preferable to have these all in a separate "build" solution to avoid complexity,
                // but we'll do that later
                var directDatadogTraceReferences = Solution
                                                  .AllProjects
                                                  .Where(project => project.SolutionFolder.Name == "instrumentation"
                                                                 && project.ReferencesDatadogTrace()
                                                                 && project.TryGetTargetFrameworks()?.Contains(Framework) != false);

                foreach (var project in directDatadogTraceReferences)
                {
                    DotnetBuild(project, framework: Framework);
                }
            }
        });

    Target PublishIisSamples => _ => _
        .Unlisted()
        .After(CompileManagedTestHelpers)
        .After(CompileSamples)
        .OnlyWhenStatic(() => IsWin)
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

    Target RunIntegrationTests => _ => _
        .Unlisted()
        .After(BuildTracerHome)
        .After(CompileIntegrationTests)
        .After(CompileSamples)
        .After(CompileTrimmingSamples)
        .After(BuildWindowsIntegrationTests)
        .After(CompileLinuxOrOsxIntegrationTests)
        .DependsOn(CleanTestLogs)
        .Requires(() => Framework)
        .Triggers(PrintSnapshotsDiff)
        .Executes(() =>
        {
            var isDebugRun = IsDebugRun();
            var filter = AddAreaFilter(GetFilter());

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
                    .SetIsDebugRun(isDebugRun)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetProcessEnvironmentVariable("USE_FULL_TEST_CONFIG", RequiresThoroughTesting().ToString())
                    .SetLogsDirectory(TestLogsDirectory)
                    // Don't apply a custom filter to these tests, they should all be able to be run
                    .When(!string.IsNullOrWhiteSpace(AddAreaFilter(Filter)), c => c.SetFilter(AddAreaFilter(Filter)))
                    .When(TestAllPackageVersions, o => o.SetProcessEnvironmentVariable("TestAllPackageVersions", "true"))
                    .When(CodeCoverageEnabled, ConfigureCodeCoverage)
                    .CombineWith(ParallelIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .WithDatadogLogger()
                        .SetProjectFile(project)), degreeOfParallelism: 4);

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
                    .SetIsDebugRun(isDebugRun)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetProcessEnvironmentVariable("USE_FULL_TEST_CONFIG", RequiresThoroughTesting().ToString())
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(!string.IsNullOrWhiteSpace(filter), c => c.SetFilter(filter))
                    .When(TestAllPackageVersions, o => o.SetProcessEnvironmentVariable("TestAllPackageVersions", "true"))
                    .When(CodeCoverageEnabled, ConfigureCodeCoverage)
                    .CombineWith(ClrProfilerIntegrationTests, (s, project) => s
                        .EnableTrxLogOutput(GetResultsDirectory(project))
                        .WithDatadogLogger()
                        .SetProjectFile(project)));
            }
            finally
            {
                CopyDumpsToBuildData();
            }

            string GetFilter()
            {
                var dockerFilter = IncludeTestsRequiringDocker switch
                {
                    true => "&(RequiresDockerDependency=true)",
                    false => "&(RequiresDockerDependency!=true)",
                    null => string.Empty,
                };

                var armFilter = IsArm64 ? "&(Category!=ArmUnsupported)" : string.Empty;

                var filter = (string.IsNullOrWhiteSpace(Filter), IsWin) switch
                {
                    (false, _) => $"({Filter})&(SkipInCI!=True){dockerFilter}{armFilter}",
                    (true, false) => $"(Category!=LinuxUnsupported)&(Category!=Lambda)&(Category!=AzureFunctions)&(SkipInCI!=True){dockerFilter}{armFilter}",
                    // TODO: I think we should change this filter to run on Windows by default, e.g.
                    // (RunOnWindows!=False|Category=Smoke)&LoadFromGAC!=True&IIS!=True
                    (true, true) => "(RunOnWindows=True)&(LoadFromGAC!=True)&(IIS!=True)&(Category!=AzureFunctions)&(SkipInCI!=True)",
                };

                return filter;
            }
        });

    private string AddAreaFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(Area))
        {
            return filter;
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            return $"(Area={Area})";
        }

        return filter + $"&(Area={Area})";
    }

    Target CompileAzureFunctionsSamplesWindows => _ => _
        .Unlisted()
        .DependsOn(HackForMissingMsBuildLocation)
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
                    return project.TryGetTargetFrameworks() switch
                    {
                        { } targets => targets.Contains(Framework),
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
        .DependsOn(CleanTestLogs)
        .Requires(() => IsWin)
        .Requires(() => Framework)
        .Triggers(PrintSnapshotsDiff)
        .Executes(() =>
        {
            var isDebugRun = IsDebugRun();
            var project = Solution.GetProject(Projects.ClrProfilerIntegrationTests);
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
                    .SetFilter(string.IsNullOrWhiteSpace(Filter) ? "(RunOnWindows=True)&(Category=AzureFunctions)&(SkipInCI!=True)" : Filter)
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetIsDebugRun(isDebugRun)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverageEnabled, ConfigureCodeCoverage)
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
        .After(CompileSamples)
        .After(CompileTrimmingSamples)
        .After(BuildNativeLoader)
        .DependsOn(CleanTestLogs)
        .Requires(() => IsWin)
        .Requires(() => Framework)
        .Executes(() =>
        {
            var isDebugRun = IsDebugRun();
            var filter = AddAreaFilter(string.IsNullOrWhiteSpace(Filter) ? "(Category=Smoke)&(LoadFromGAC!=True)&(Category!=AzureFunctions)&(SkipInCI!=True)" : Filter);

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
                    .SetFilter(filter)
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetIsDebugRun(isDebugRun)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverageEnabled, ConfigureCodeCoverage)
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
        .After(PublishIisSamples)
        .Triggers(PrintSnapshotsDiff)
        .Requires(() => Framework)
        .Executes(() => RunWindowsIisIntegrationTests(
                      Solution.GetProject(Projects.ClrProfilerIntegrationTests)))
        .Executes(() => RunWindowsIisIntegrationTests(
                      Solution.GetProject(Projects.AppSecIntegrationTests)));

    void RunWindowsIisIntegrationTests(Project project)
    {
        var isDebugRun = IsDebugRun();
        EnsureResultsDirectory(project);
        var filter = AddAreaFilter(string.IsNullOrWhiteSpace(Filter) ? "(RunOnWindows=True)&(LoadFromGAC=True)&(Category!=AzureFunctions)&(SkipInCI!=True)" : Filter);

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
                                .SetFilter(filter)
                                .SetTestTargetPlatform(TargetPlatform)
                                .SetIsDebugRun(isDebugRun)
                                .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                                .SetLogsDirectory(TestLogsDirectory)
                                .When(CodeCoverageEnabled, ConfigureCodeCoverage)
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
        .After(PublishIisSamples)
        .Triggers(PrintSnapshotsDiff)
        .Requires(() => Framework)
        .Executes(() =>
        {
            var isDebugRun = IsDebugRun();
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
                    .SetFilter(string.IsNullOrWhiteSpace(Filter) ? "(RunOnWindows=True)&(MSI=True)&(Category!=AzureFunctions)&(SkipInCI!=True)" : Filter)
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetIsDebugRun(isDebugRun)
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverageEnabled, ConfigureCodeCoverage)
                    .EnableTrxLogOutput(resultsDirectory)
                    .WithDatadogLogger()
                    .SetProjectFile(project));
            }
            finally
            {
                CopyDumpsToBuildData();
            }
        });

    Target HackForMissingMsBuildLocation => _ => _
       .Unlisted()
       .Executes(() =>
        {
            // This shouldn't be necessary, but without it we get msbuild location errors on Linux/macOs :shrug:
            ProjectModelTasks.Initialize();
        });

    Target CompileLinuxOrOsxIntegrationTests => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileManagedTestHelpers)
        .After(BuildRunnerTool)
        .Requires(() => MonitoringHomeDirectory != null)
        .Requires(() => Framework)
        .Executes(() =>
        {
            // Build the actual integration test projects for Any CPU
            var integrationTestProjects =
                TracerDirectory
                   .GlobFiles("test/*.IntegrationTests/*.csproj")
                   .Where(path => !((string)path).Contains(Projects.DebuggerIntegrationTests))
                   .Where(path => !((string)path).Contains(Projects.DdDotnetIntegrationTests));

            DotnetBuild(integrationTestProjects, framework: Framework, noRestore: false);

            IntegrationTestLinuxOrOsxProfilerDirFudge(Projects.ClrProfilerIntegrationTests);
            IntegrationTestLinuxOrOsxProfilerDirFudge(Projects.AppSecIntegrationTests);
        });

    Target CompileLinuxDdDotnetIntegrationTests => _ => _
        .Unlisted()
        .After(CompileManagedSrc)
        .After(CompileManagedTestHelpers)
        .Requires(() => MonitoringHomeDirectory != null)
        .Executes(() =>
        {
            DotnetBuild(Solution.GetProject(Projects.DdDotnetIntegrationTests), noRestore: false);
        });

    Target RunLinuxDdDotnetIntegrationTests => _ => _
        .After(CompileLinuxOrOsxIntegrationTests)
        .DependsOn(CleanTestLogs)
        .Description("Runs the linux dd-dotnet integration tests")
        .Requires(() => !IsWin)
        .Executes(() =>
        {
            var project = Solution.GetProject(Projects.DdTraceIntegrationTests);
            EnsureResultsDirectory(project);

            try
            {
                DotNetTest(config => config
                    .SetConfiguration(BuildConfiguration)
                    .EnableNoRestore()
                    .EnableNoBuild()
                    .EnableCrashDumps()
                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                    .SetTestTargetPlatform(TargetPlatform)
                    .SetLogsDirectory(TestLogsDirectory)
                    .When(CodeCoverageEnabled, ConfigureCodeCoverage)
                    .SetProjectFile(Projects.DdTraceIntegrationTests)
                    .EnableTrxLogOutput(project)
                    .WithDatadogLogger());
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
                 Logger.Information("Could not uninstall the dd-trace tool. It's probably not installed.");
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
              DotnetBuild(Solution.GetProject(Projects.DdTraceArtifactsTests));
          });

    Target BuildDdDotnetArtifactTests => _ => _
     .Description("Builds the dd-dotnet artifacts tests")
     .After(CompileManagedTestHelpers)
     .Requires(() => Framework)
     .Executes(() =>
     {
         DotnetBuild(Solution.GetProject(Projects.DdDotnetArtifactsTests), Framework);

         // Compile the required samples
         var sampleProjects = new List<AbsolutePath>
         {
             TracerDirectory / "test/test-applications/integrations/Samples.Console/Samples.Console.csproj",
             TracerDirectory / "test/test-applications/integrations/Samples.VersionConflict.1x/Samples.VersionConflict.1x.csproj"
         };

         if (!IsWin)
         {
             sampleProjects.Add(TracerDirectory / "test/test-applications/integrations/Samples.AspNetCoreMinimalApis/Samples.AspNetCoreMinimalApis.csproj");
         }

         // do the build and publish separately to avoid dependency issues
         DotnetBuild(sampleProjects, framework: Framework, noRestore: false);

         DotNetPublish(x => x
            .EnableNoRestore()
            .EnableNoBuild()
            .EnableNoDependencies()
            .SetConfiguration(BuildConfiguration)
            .SetFramework(Framework)
            .SetNoWarnDotNetCore3()
            .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
            .CombineWith(sampleProjects, (c, project) => c
                .SetProject(project)));
     });

    Target RunToolArtifactTests => _ => _
       .Description("Runs the tool artifacts tests")
       .After(BuildToolArtifactTests)
       .Executes(() =>
        {
            var isDebugRun = IsDebugRun();
            var project = Solution.GetProject(Projects.DdTraceArtifactsTests);

            DotNetTest(config => config
                .SetProjectFile(project)
                .SetConfiguration(BuildConfiguration)
                .EnableNoRestore()
                .EnableNoBuild()
                .SetIsDebugRun(isDebugRun)
                .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                .SetProcessEnvironmentVariable("ToolInstallDirectory", ToolInstallDirectory)
                .SetLogsDirectory(TestLogsDirectory)
                .EnableTrxLogOutput(GetResultsDirectory(project))
                .WithDatadogLogger());
        });

    Target RunDdDotnetArtifactTests => _ => _
       .Description("Runs the dd-dotnet artifacts tests")
       .After(BuildDdDotnetArtifactTests)
       .After(CopyDdDotnet)
       .Executes(() =>
       {
           try
           {
               var isDebugRun = IsDebugRun();
               var project = Solution.GetProject(Projects.DdDotnetArtifactsTests);

               DotNetTest(config => config
                       .SetProjectFile(project)
                       .SetDotnetPath(TargetPlatform)
                       .SetConfiguration(BuildConfiguration)
                       .SetFramework(Framework)
                       .SetTestTargetPlatform(TargetPlatform)
                       .EnableNoRestore()
                       .EnableNoBuild()
                       .SetIsDebugRun(isDebugRun)
                       .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                       .SetLogsDirectory(TestLogsDirectory)
                       .EnableTrxLogOutput(GetResultsDirectory(project))
                       .WithDatadogLogger());
           }
           finally
           {
               CopyDumpsToBuildData();
           }
       });

    Target CopyServerlessArtifacts => _ => _
       .Description("Copies monitoring-home into the serverless artifacts directory")
       .Unlisted()
       .After(CompileSamples, CompileTrimmingSamples)
       .Executes(() =>
        {
            // This is a bit hacky, we can probably improve it once/if we output monitoring home into the BuildArtifactsDirectory too
            var serverlessProjects = new List<string> { "Samples.AWS.Lambda", "Samples.Amazon.Lambda.RuntimeSupport" };
            foreach (var project in serverlessProjects)
            {
                var rootSampleFolder = BuildArtifactsDirectory / "bin" / project;
                CopyDirectoryRecursively(MonitoringHomeDirectory, rootSampleFolder / "monitoring-home", DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
                CopyFileToDirectory(
                    source: TracerDirectory / "build" / "_build" / "docker" / "serverless.lambda.dockerfile",
                    targetDirectory: rootSampleFolder,
                    FileExistsPolicy.Skip);
            }
        });

    Target CreateMissingNullabilityFile => _ => _
        .Description("Create missing-nullability-files.csv file for tracking nullability in the repo")
        .Executes(() =>
        {
            var include = TracerDirectory.GlobFiles("src/Datadog.Trace/**/*.cs");
            var exclude = TracerDirectory.GlobFiles(
                "src/Datadog.Trace/obj/**",
                "src/Datadog.Trace/Generated/**",
                "src/Datadog.Trace/Vendors/**"
            );

            int ComputeDepth(AbsolutePath ap)
            {
                var d = 0;
                while ((ap = ap.Parent) != null)
                {
                    d++;
                }

                return d;
            }

            string NormalizedPath(AbsolutePath ap)
            {
                // paths are not printed the same way in windows vs unix-based, which affects the ordering.
                // to get a stable order, we use / everywhere, which is what's written in the csv in the end.
                return ap.ToString().Replace(oldChar: '\\', newChar: '/');
            }

            var sourceFiles = include.Except(exclude).ToList();
            // sort by depth, then alphabetical.
            sourceFiles.Sort((a, b) =>
            {
                var depthDiff = ComputeDepth(a) - ComputeDepth(b);
                return depthDiff != 0 ? depthDiff : string.Compare(NormalizedPath(a), NormalizedPath(b), StringComparison.OrdinalIgnoreCase);
            });

            var sb = new StringBuilder();
            foreach (var file in sourceFiles)
            {
                bool missingNullability = true;

                foreach (var line in File.ReadLines(file))
                {
                    if (line.Contains("#nullable enable"))
                    {
                        missingNullability = false;
                        break;
                    }
                }

                if (missingNullability)
                {
                    sb.AppendLine(TracerDirectory.GetUnixRelativePathTo(file));
                }
            }

            var csvFilePath = TracerDirectory / "missing-nullability-files.csv";
            File.WriteAllText(csvFilePath, sb.ToString());
            Logger.Information("File ordered and saved: {File}", csvFilePath);
        });

    Target CreateTrimmingFile => _ => _
       .Description("Create Datadog.Trace.Trimming.xml file")
       .After(CompileManagedSrc)
       .Executes(() =>
        {
            var loaderTypes = GetTypeReferences(SourceDirectory / "bin" / "ProfilerResources" / "netcoreapp2.0" / "Datadog.Trace.ClrProfiler.Managed.Loader.dll");
            List<(string Assembly, string Type)> datadogTraceTypes = new();
            foreach (var tfm in AppTrimmingTFMs)
            {
                datadogTraceTypes.AddRange(GetTypeReferences(DatadogTraceDirectory / "bin" / BuildConfiguration / tfm / Projects.DatadogTrace + ".dll"));
            }

            // add Datadog projects to the root descriptors file
            datadogTraceTypes.Add(new(Projects.DatadogTrace, null));

            var types = loaderTypes
                       .Concat(datadogTraceTypes)
                       .Distinct()
                       .OrderBy(t => t.Assembly)
                       .ThenBy(t => t.Type);

            var sb = new StringBuilder(65_536);
            sb.AppendLine("<linker>");
            foreach (var module in types.GroupBy(g => g.Assembly))
            {
                if (module.Count() == 1 && module.First().Type == null)
                {
                    sb.AppendLine($"   <assembly fullname=\"{module.Key}\" />");
                }
                else
                {
                    sb.AppendLine($"   <assembly fullname=\"{module.Key}\">");
                    foreach (var type in module)
                    {
                        if (!string.IsNullOrEmpty(type.Type))
                        {
                            sb.AppendLine($"      <type fullname=\"{type.Type}\" />");
                        }
                    }

                    sb.AppendLine("""   </assembly>""");
                }
            }

            sb.AppendLine("</linker>");

            var projectFolder = Solution.GetProject(Projects.DatadogTraceTrimming).Directory;
            var descriptorFilePath = projectFolder / "build" / $"{Projects.DatadogTraceTrimming}.xml";
            File.WriteAllText(descriptorFilePath, sb.ToString());
            Logger.Information("File saved: {File}", descriptorFilePath);

            static List<(string Assembly, string Type)> GetTypeReferences(string dllPath)
            {
                // We check if the assembly file exists.
                if (!File.Exists(dllPath))
                {
                    throw new FileNotFoundException($"Error extracting types for trimming support. Assembly file was not found. Path: {dllPath}", dllPath);
                }

                // Open dll to extract all referenced types from the assembly (TypeRef table)
                using var asmDefinition = Mono.Cecil.AssemblyDefinition.ReadAssembly(dllPath);
                var lst = new List<(string Assembly, string Type)>(asmDefinition.MainModule.GetTypeReferences().Select(t => (t.Scope.Name, t.FullName)));

                // Get target assemblies from Calltarget integrations.
                // We need to play safe and select the complete assembly and not the type due to the impossibility
                // to extract the target types from DuckTyping proxies.
                lst.AddRange(GetTargetAssembliesFromAttributes(asmDefinition));
                return lst;
            }

            static IEnumerable<(string Assembly, string Type)> GetTargetAssembliesFromAttributes(AssemblyDefinition asmDefinition)
            {
                foreach (var type in asmDefinition.MainModule.Types)
                {
                    if (type.HasCustomAttributes)
                    {
                        foreach (var attr in type.CustomAttributes)
                        {
                            // Extract InstrumentMethodAttribute (CallTarget integrations)
                            // We need to check both properties `AssemblyName` and `AssemblyNames`
                            // because the actual data is embedded to the argument parameter in the assembly
                            // (doesn't work as a normally property works at runtime)
                            if (attr.AttributeType.FullName == "Datadog.Trace.ClrProfiler.InstrumentMethodAttribute")
                            {
                                foreach (var prp in attr.Properties)
                                {
                                    if (prp.Name == "AssemblyName" && prp.Argument.Value.ToString() is { Length: > 0 } asmValue)
                                    {
                                        yield return (asmValue, null);
                                    }

                                    if (prp.Name == "AssemblyNames" && prp.Argument.Value is Mono.Cecil.CustomAttributeArgument[] attributeArguments)
                                    {
                                        foreach (var attrArg in attributeArguments)
                                        {
                                            if (attrArg.Value?.ToString() is { Length: > 0 } attrArgValue)
                                            {
                                                yield return (attrArgValue, null);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Extract AdoNetClientInstrumentMethodsAttribute (ADO.NET CallTarget integrations)
                // We look for the target integration assembly.
                if (asmDefinition.MainModule.Assembly.HasCustomAttributes)
                {
                    foreach (var attr in asmDefinition.MainModule.Assembly.CustomAttributes)
                    {
                        if (attr.AttributeType.FullName == "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute")
                        {
                            foreach (var prp in attr.Properties)
                            {
                                if (prp.Name == "AssemblyName" && prp.Argument.Value.ToString() is { Length: > 0 } asmValue)
                                {
                                    yield return (asmValue, null);
                                }
                            }
                        }
                    }
                }
            }
        });

    Target CheckBuildLogsForErrors => _ => _
       .Unlisted()
       .Description("Reads the logs from build_data and checks for error lines")
       .Executes(async () =>
       {
           // we expect to see _some_ errors, so explicitly ignore them
           var knownPatterns = new List<Regex>
           {
               new(@".*Unable to resolve method MongoDB\..*", RegexOptions.Compiled),
               // Expected errors in CallTargetNativeTests
               new(@".*Noop\dArgumentsIntegration\.OnAsyncMethodEnd.*CallTargetNativeTest.*", RegexOptions.Compiled | RegexOptions.Singleline),
               new(@".*Noop\dArgumentsIntegration\.OnMethodBegin.*CallTargetNativeTest.*", RegexOptions.Compiled | RegexOptions.Singleline),
               new(@".*Noop\dArgumentsIntegration\.OnMethodEnd.*CallTargetNativeTest.*", RegexOptions.Compiled | RegexOptions.Singleline),
               new(@".*Noop\dArgumentsVoidIntegration\.OnMethodBegin.*CallTargetNativeTest.*", RegexOptions.Compiled | RegexOptions.Singleline),
               new(@".*Noop\dArgumentsVoidIntegration\.OnMethodEnd.*CallTargetNativeTest.*", RegexOptions.Compiled | RegexOptions.Singleline),
               new(@".*System.Threading.ThreadAbortException: Thread was being aborted\.", RegexOptions.Compiled),
               new(@".*System.InvalidOperationException: Module Samples.Trimming.dll has no HINSTANCE.*", RegexOptions.Compiled),
               // CI Visibility known errors
               new(@".*The Git repository couldn't be automatically extracted.*", RegexOptions.Compiled),
               new(@".*DD_GIT_REPOSITORY_URL is set with.*", RegexOptions.Compiled),
               new(@".*The Git commit sha couldn't be automatically extracted.*", RegexOptions.Compiled),
               new(@".*DD_GIT_COMMIT_SHA must be a full-length git SHA.*", RegexOptions.Compiled),
               new(@".*Timeout occurred when flushing spans.*", RegexOptions.Compiled),
               new(@".*TestOptimization: .*", RegexOptions.Compiled),
               new(@".*TestOptimizationClient: .*", RegexOptions.Compiled),
               // This one is annoying but we _think_ due to a dodgy named pipes implementation, so ignoring for now
               new(@".*An error occurred while sending data to the agent at \\\\\.\\pipe\\trace-.*The operation has timed out.*", RegexOptions.Compiled),
               new(@".*An error occurred while sending data to the agent at \\\\\.\\pipe\\metrics-.*The operation has timed out.*", RegexOptions.Compiled),
               new(@".*Error detecting and reconfiguring git repository for shallow clone. System.IO.FileLoadException.*", RegexOptions.Compiled),
               // These are thrown by the CallTargetNativeTests
               new(@".*Exception occurred when calling the CallTarget integration continuation. Datadog.Trace.DuckTyping.DuckTypeException: Throwing a ducktype exception.*"),
               new(@".*Exception occurred when calling the CallTarget integration continuation. System.MissingMethodException: Throwing a missing method exception.*"),
               new(@".*Exception occurred when calling the CallTarget integration continuation. Datadog.Trace.ClrProfiler.CallTarget.CallTargetInvokerException: Throwing a call target invoker exception.*"),
               // error thrown to check error handling in RC tests
               new(@".*haha, you weren't expect this!*", RegexOptions.Compiled),
               // known errors in waf config
               new(@".*rc::asm_dd::diagnostic Error: missing key.*", RegexOptions.Compiled),
               new(@".*rc::asm_dd::diagnostic Warning: unknown operator.*", RegexOptions.Compiled),
               new(@".*Some errors were found while applying waf configuration \(RulesFile: wrong-tags-name-rule-set.json\).*", RegexOptions.Compiled),
               new(@".*Some errors were found while applying waf configuration \(RulesFile: rasp-rule-set.json\).*", RegexOptions.Compiled),
           };

           await CheckLogsForErrors(knownPatterns, allFilesMustExist: false, minLogLevel: LogLevel.Error, new ());
       });

    Target CheckSmokeTestsForErrors => _ => _
       .Unlisted()
       .Description("Reads the logs from build_data and checks for error lines in the smoke test logs")
       .Executes(async () =>
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
           }

           // We disable the profiler in crash tests, so we expect these logs
           knownPatterns.Add(new(@".*Error getting IClassFactory from: .*/Datadog\.Profiler\.Native\.so", RegexOptions.Compiled));
           knownPatterns.Add(new(@".*DynamicDispatcherImpl::LoadClassFactory: Error trying to load continuous profiler class factory.*", RegexOptions.Compiled));
           knownPatterns.Add(new(@".*Error loading all cor profiler class factories\.", RegexOptions.Compiled));

           // profiler occasionally throws this if shutting down
           knownPatterns.Add(new(@".*LinuxStackFramesCollector::CollectStackSampleImplementation: Unable to send signal .*Error code: No such process", RegexOptions.Compiled));
           // profiler throws this on .NET 7 - currently allowed
           knownPatterns.Add(new(@".*Profiler call failed with result Unspecified-Failure \(80131351\): pInfo..GetModuleInfo\(moduleId, nullptr, 0, nullptr, nullptr, .assemblyId\)", RegexOptions.Compiled));
           // avoid any issue with CLR events that are not supported before 5.1 or .NET Framework
           knownPatterns.Add(new(@".*Event-based profilers \(Allocation, LockContention\) are not supported for", RegexOptions.Compiled));
           // There's a race in the profiler where unwinding when the thread is running
           knownPatterns.Add(new(@".*Failed to walk \d+ stacks for sampled exception:\s+CORPROF_E_STACKSNAPSHOT_UNSAFE", RegexOptions.Compiled));

           // This one is caused by the intentional crash in the crash tracking smoke test
           knownPatterns.Add(new("Application threw an unhandled exception: System.BadImageFormatException: Expected", RegexOptions.Compiled));

           // We intentionally set the variables for smoke tests which means we get this warning on <= .NET Core 3.0 or <.NET 6.0.12
           knownPatterns.Add(new(".*SingleStepGuardRails::ShouldForceInstrumentationOverride: Found incompatible runtime .NET Core 3.0 or lower", RegexOptions.Compiled));
           knownPatterns.Add(new(".*SingleStepGuardRails::ShouldForceInstrumentationOverride: Found incompatible runtime .NET 6.0.12 and earlier have known crashing bugs", RegexOptions.Compiled));

           // Make sure we _only_ add this while .NET 10 is in preview (to make sure we don't forget in the final release)
           if (RuntimeInformation.FrameworkDescription.StartsWith(".NET 10.0.0-"))
           {
               knownPatterns.Add(new(@".*SingleStepGuardRails::ShouldForceInstrumentationOverride: Found incompatible runtime .NET 10 or higher.*", RegexOptions.Compiled));
           }

           // CI Visibility known errors
           knownPatterns.Add(new(@".*The Git repository couldn't be automatically extracted.*", RegexOptions.Compiled));
           knownPatterns.Add(new(@".*DD_GIT_REPOSITORY_URL is set with.*", RegexOptions.Compiled));
           knownPatterns.Add(new(@".*The Git commit sha couldn't be automatically extracted.*", RegexOptions.Compiled));
           knownPatterns.Add(new(@".*DD_GIT_COMMIT_SHA must be a full-length git SHA.*", RegexOptions.Compiled));
           knownPatterns.Add(new(@".*Timeout occurred when flushing spans.*", RegexOptions.Compiled));
           knownPatterns.Add(new(@".*TestOptimization: .*", RegexOptions.Compiled));
           knownPatterns.Add(new(@".*TestOptimizationClient: .*", RegexOptions.Compiled));

           // glibc TLS-reuse bug warnings
           knownPatterns.Add(new(@".*GLIBC version 2.34-2.36 has a TLS-reuse bug.*", RegexOptions.Compiled));

           // These patterns should be ignored, but we should send a metric when they occur
           // so that we can track they don't happen too often and gate releases on them etc
           var reportablePatterns = new List<(string IgnoreReasonTag, Regex Regex)>
           {
               new("rejit_thread_timeout", new(@".*Timeout while waiting for the rejit requests to be processed. Rejit will continue asynchronously, but some initial calls may not be instrumented.*", RegexOptions.Compiled))
           };

           await CheckLogsForErrors(knownPatterns, allFilesMustExist: true, minLogLevel: LogLevel.Warning, reportablePatterns);
       });

    Target ExtractMetricsFromLogs => _ => _
       .Unlisted()
       .Description("Reads the logs from build_data, extracts the metrics, and submits them to Datadog")
       .Executes(async () =>
       {
           var logDirectory = BuildDataDirectory / "logs";
           await LogParser.ReportNativeMetrics(logDirectory);
       });

    private async Task CheckLogsForErrors(List<Regex> knownPatterns, bool allFilesMustExist, LogLevel minLogLevel, List<(string IgnoreReasonTag, Regex Regex)> reportablePatterns)
    {
        var logDirectory = BuildDataDirectory / "logs";
        if (await LogParser.DoLogsContainErrors(logDirectory, knownPatterns, allFilesMustExist, minLogLevel, reportablePatterns))
        {
            ExitCode = 1;
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

    private (string Arch, string Ext) GetUnixArchitectureAndExtension() => GetUnixArchitectureAndExtension(IsOsx, IsAlpine);
    private (string Arch, string Ext) GetUnixArchitectureAndExtension(bool isOsx, bool isAlpine) =>
        (isOsx, isAlpine) switch
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
        var dumpFolder = root / "dumps";

        if (Directory.Exists(TempDirectory))
        {
            foreach (var dump in GlobFiles(TempDirectory, "coredump*", "*.dmp", "*.crashreport.json"))
            {
                Logger.Information("Moving file '{Dump}' to '{Root}'", dump, dumpFolder);

                CopyFileToDirectory(dump, dumpFolder, FileExistsPolicy.OverwriteIfNewer);
            }
        }

        foreach (var file in Directory.EnumerateFiles(TracerDirectory, "*.dmp", SearchOption.AllDirectories))
        {
            if (Path.GetDirectoryName(file) == dumpFolder)
            {
                // The dump is already in the right location
                continue;
            }

            CopyFileToDirectory(file, dumpFolder, FileExistsPolicy.OverwriteIfNewer);
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

    protected override void OnTargetRunning(string target)
    {
        if (PrintDriveSpace)
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                Logger.Information($"Drive space available on '{drive.Name}': {PrettyPrint(drive.AvailableFreeSpace)} / {PrettyPrint(drive.TotalSize)}");
            }
        }

        base.OnTargetRunning(target);

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


    private void DotnetBuild(
        Project project,
        TargetFramework framework = null,
        bool noRestore = true,
        bool noDependencies = true
        )
    {
        DotnetBuild(new[] { project.Path }, framework, noRestore, noDependencies);
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


    private async Task<string> GetVcpkg()
    {
        var vcpkgFilePath = string.Empty;

        try
        {
            vcpkgFilePath = ToolPathResolver.GetPathExecutable("vcpkg.exe");
        }
        catch (ArgumentException)
        { }

        if (File.Exists(vcpkgFilePath))
        {
            return vcpkgFilePath;
        }

        // Check if already downloaded
        var vcpkgRoot = RootDirectory / "artifacts" / "bin" / "vcpkg";
        var vcpkgExecPath = vcpkgRoot / "vcpkg.exe";
        var bootstrap = vcpkgRoot / "bootstrap-vcpkg.bat";

        if (File.Exists(vcpkgExecPath))
        {
            return $"{vcpkgExecPath}";
        }

        await DownloadAndExtractVcpkg(vcpkgRoot);
        Cmd.Value(arguments: $@"cmd /c ""{bootstrap}""");
        return $"{vcpkgRoot / "vcpkg.exe"}";
    }

    private async Task DownloadAndExtractVcpkg(AbsolutePath destinationFolder)
    {
        var nbTries = 0;
        var keepTrying = true;
        var vcpkgZip = TempDirectory / "vcpkg.zip";
        using var client = new HttpClient();
        const string vcpkgVersion = "2024.11.16";
        while (keepTrying)
        {
            nbTries++;
            try
            {
                var response = await client.GetAsync($"https://github.com/microsoft/vcpkg/archive/refs/tags/{vcpkgVersion}.zip");
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var file = File.Create(vcpkgZip);
                await stream.CopyToAsync(file);
                keepTrying = false;
            }
            catch (HttpRequestException)
            {
                if (nbTries > 3)
                {
                    throw;
                }
            }
        }

        EnsureExistingParentDirectory(destinationFolder);
        var parentFolder = destinationFolder.Parent;

        UncompressZipQuiet(vcpkgZip, parentFolder);

        RenameDirectory(parentFolder / $"vcpkg-{vcpkgVersion}", destinationFolder.Name);
    }

    // Quiet version of CompressionTasks:UncompressZip, which displays a log line for every created directory when uncompressing
    private static void UncompressZipQuiet(string archiveFile, string directory)
    {
        Logger.Information("Uncompressing {File} to {Directory} ...", Path.GetFileName(archiveFile), directory);

        using var fileStream = File.OpenRead(archiveFile);
        using var zipFile = new ZipFile(fileStream);

        var entries = zipFile.Cast<ZipEntry>().Where(x => !x.IsDirectory);
        foreach (var entry in entries)
        {
            var file = PathConstruction.Combine(directory, entry.Name);
            var path = Path.GetDirectoryName(file);

            if (!Directory.Exists(path) && !string.IsNullOrEmpty(path))
                {
                    Directory.CreateDirectory(path);
                }

            using var entryStream = zipFile.GetInputStream(entry);
            using var outputStream = File.Open(file, FileMode.Create);
            entryStream.CopyTo(outputStream);
        }
    }

    public static class LibdatadogLogParser
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        public static LogEntry ParseEntry(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<LogEntry>(json, Options);
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Failed to parse JSON: {ex.Message}");
                return null;
            }
        }

        public record LogEntry(
            DateTimeOffset Timestamp,
            string Level,
            Dictionary<string, object> Fields,
            string Target,
            string Filename,
            // ReSharper disable once InconsistentNaming
            int? Line_number,
            string ThreadName,
            string ThreadId
        );
    }
}
