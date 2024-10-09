using System;
using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.IO;
using System.Linq;
using System.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MSBuild;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using Nuke.Common.Tools.NuGet;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Nuke.Common.Utilities;
using System.Collections;
using System.Threading.Tasks;
using DiffMatchPatch;
using Logger = Serilog.Log;

partial class Build
{
    const string ClangTidyChecks = "-clang-analyzer-osx*,-clang-analyzer-optin.osx*,-cppcoreguidelines-avoid-magic-numbers,-cppcoreguidelines-pro-type-vararg,-readability-braces-around-statements";

    AbsolutePath ProfilerDeployDirectory => ProfilerOutputDirectory / "DDProf-Deploy";

    Target CompileProfilerNativeSrc => _ => _
        .Unlisted()
        .Description("Compiles the native profiler assets")
        .DependsOn(CompileProfilerNativeSrcWindows)
        .DependsOn(CompileProfilerNativeSrcLinux);

    Target CompileProfilerNativeTests => _ => _
        .Unlisted()
        .Description("Compiles the native profiler assets")
        .DependsOn(CompileProfilerNativeTestsWindows)
        .DependsOn(CompileProfilerNativeTestsLinux);

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

    Target CompileProfilerNativeTestsLinux => _ => _
        .Unlisted()
        .Description("Compile Profiler native code")
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target profiler-native-tests");

            if (IsAlpine)
            {
                // On Alpine, we do not have permission to access the file libunwind-prefix/src/libunwind/config/config.guess
                // Make the whole folder and its content accessible by everyone to make sure the upload process does not fail
                Chmod.Value.Invoke(" -R 777 " + NativeBuildDirectory);
            }
        });

    Target CompileProfilerNativeSrcLinux => _ => _
        .Unlisted()
        .Description("Compile Profiler native code")
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target profiler");

            if (IsAlpine)
            {
                // On Alpine, we do not have permission to access the file libunwind-prefix/src/libunwind/config/config.guess
                // Make the whole folder and its content accessible by everyone to make sure the upload process does not fail
                Chmod.Value.Invoke(" -R 777 " + NativeBuildDirectory);
            }
        });

    Target CompileNativeWrapper => _ => _
        .Unlisted()
        .Description("Compile Native wrapper")
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            var additionalArgs = $"-DUNIVERSAL={(AsUniversal ? "ON" : "OFF")}";

            if (AsUniversal)
            {
                additionalArgs += $" -DCMAKE_TOOLCHAIN_FILE=./build/cmake/Universal.cmake.{(IsArm64 ? "aarch64" : "x86_64")}";
            }

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration} {additionalArgs}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target wrapper");
        });

    Target TestNativeWrapper => _ => _
        .Unlisted()
        .Description("Test that the Native wrapper symbols haven't changed")
        .After(CompileNativeWrapper)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var (arch, _) = GetUnixArchitectureAndExtension();
            var libraryPath = ProfilerDeployDirectory / arch / FileNames.ProfilerLinuxApiWrapper;

            var output = Nm.Value($"-D {libraryPath}").Select(x => x.Text).ToList();

            // Gives output similar to this:
            // 0000000000006bc8 D DdDotnetFolder
            // 0000000000006bd0 D DdDotnetMuslFolder
            //                  w _ITM_deregisterTMCloneTable
            //                  w _ITM_registerTMCloneTable
            //                  w __cxa_finalize
            //                  w __deregister_frame_info
            //                  U __errno_location
            //                  U __tls_get_addr
            // 0000000000002d1b T _fini
            // 0000000000002d18 T _init
            // 0000000000003d70 T accept
            // 0000000000003e30 T accept4
            //                  U access
            //
            // The types of symbols are:
            // D: Data section symbol. These symbols are initialized global variables.
            // w: Weak symbol. These symbols are weakly referenced and can be overridden by other symbols.
            // U: Undefined symbol. These symbols are referenced in the file but defined elsewhere.
            // T: Text section symbol. These symbols are functions or executable code.
            // B: BSS (Block Started by Symbol) section symbol. These symbols are uninitialized global variables.
            //
            // We only care about the Undefined symbols - we don't want to accidentally add more of them

            Logger.Debug("NM output: {Output}", string.Join(Environment.NewLine, output));

            var symbols = output
                .Select(x => x.Trim())
                .Where(x => x.StartsWith("U "))
                .Select(x => x.TrimStart("U "))
                .OrderBy(x => x)
                .ToList();


            var received = string.Join(Environment.NewLine, symbols);
            var verifiedPath = TestsDirectory / "snapshots" / $"native-wrapper-symbols-{UnixArchitectureIdentifier}.verified.txt";
            var verified = File.Exists(verifiedPath)
                ? File.ReadAllText(verifiedPath)
                : string.Empty;

            Logger.Information("Comparing snapshot of Undefined symbols in the Native Wrapper library using {Path}...", verifiedPath);

            var dmp = new diff_match_patch();
            var diff = dmp.diff_main(verified, received);
            dmp.diff_cleanupSemantic(diff);

            var changedSymbols = diff
                .Where(x => x.operation != Operation.EQUAL)
                .Select(x => x.text.Trim())
                .ToList();

            if (changedSymbols.Count == 0)
            {
                Logger.Information("No changes found in Undefined symbols in the Native Wrapper library");
                return;
            }

            PrintDiff(diff);

            throw new Exception($"Found differences in undefined symbols ({string.Join(",", changedSymbols)}) in the Native Wrapper library. " +
                                "Verify that these changes are expected, and will not cause problems. " +
                                "Removing symbols is generally a safe operation, but adding them could cause crashes. " +
                                $"If the new symbols are safe to add, update the snapshot file at {verifiedPath} with the " +
                                "new values");
        });

    Target CompileNativeWrapperNativeTests => _ => _
        .Unlisted()
        .Description("Compile Native wrapper unit tests")
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target wrapper-native-tests");
        });

    Target RunProfilerNativeUnitTestsLinux => _ => _
        .Unlisted()
        .Description("Run profiler native unit tests")
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileProfilerNativeTestsLinux)
        .Executes(() =>
        {
            RunProfilerUnitTests("Datadog.Profiler.Native.Tests", Configuration.Release, MSBuildTargetPlatform.x64, SanitizerKind.None);
        });

    Target RunNativeWrapperNativeTests => _ => _
        .Unlisted()
        .Description("Run native wrapper unit tests")
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileNativeWrapperNativeTests)
        .Executes(() =>
        {
            // LD_PRELOAD must be set for this test library to validate that it works correctly.
            var (arch, _) = GetUnixArchitectureAndExtension();
            var envVars = new[] { $"LD_PRELOAD={ProfilerDeployDirectory / arch / FileNames.ProfilerLinuxApiWrapper}" };
            RunProfilerUnitTests("Datadog.Linux.ApiWrapper.Tests", Configuration.Release, MSBuildTargetPlatform.x64, SanitizerKind.None, envVars);
        });

    Target CompileProfilerNativeTestsWindows => _ => _
        .Unlisted()
        .After(CompileProfilerNativeSrcWindows)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // If we're building for x64, build for x86 too
            var platforms =
                Equals(TargetPlatform, MSBuildTargetPlatform.x64)
                    ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                    : new[] { MSBuildTargetPlatform.x86 };

            var testProjects = ProfilerDirectory.GlobFiles("test/**/*.vcxproj");
            NuGetTasks.NuGetRestore(s => s
                .SetTargetPath(ProfilerMsBuildProject)
                .SetVerbosity(NuGetVerbosity.Normal)
                .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackagesDirectory(NugetPackageDirectory))
                .CombineWith(testProjects, (m, testProjects) => m.SetTargetPath(testProjects)));

            // Can't use dotnet msbuild, as needs to use the VS version of MSBuild
            MSBuild(s => s
                .SetTargetPath(ProfilerMsBuildProject)
                .SetConfiguration(BuildConfiguration)
                .SetMSBuildPath()
                .SetTargets("BuildCppTestsOnly")
                .DisableRestore()
                .SetMaxCpuCount(null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target RunProfilerNativeUnitTestsWindows => _ => _
        .Unlisted()
        .After(CompileProfilerNativeTestsWindows)
        .After(CompileProfilerWithAsanWindows)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatformForProfiler)
            {
                RunProfilerUnitTests("Datadog.Profiler.Native.Tests", BuildConfiguration, architecture);
            }
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
            var (arch, _) = GetUnixArchitectureAndExtension();
            var sourceDir = ProfilerDeployDirectory / arch;
            EnsureExistingDirectory(MonitoringHomeDirectory / arch);

            var files = new[] { "Datadog.Profiler.Native.so" };
            foreach (var file in files)
            {
                var source = sourceDir / file;
                var dest = MonitoringHomeDirectory / arch / file;
                CopyFile(source, dest, FileExistsPolicy.Overwrite);
            }
        });

    Target PublishNativeWrapper => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileNativeWrapper)
        .Executes(() =>
        {
            var (arch, _) = GetUnixArchitectureAndExtension();
            var sourceDir = ProfilerDeployDirectory / arch;
            EnsureExistingDirectory(MonitoringHomeDirectory / arch);

            var source = sourceDir / FileNames.ProfilerLinuxApiWrapper;
            var dest = MonitoringHomeDirectory / arch / FileNames.ProfilerLinuxApiWrapper;
            CopyFile(source, dest, FileExistsPolicy.Overwrite);

            if (AsUniversal)
            {
                var libc = IsArm64 ? "libc.musl-aarch64.so.1" : "libc.musl-x86_64.so.1";
                PatchElf.Value.Invoke($"--remove-needed {libc} {dest} --remove-rpath");
            }
        });

    Target PublishProfilerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileProfilerNativeSrc)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatformForProfiler)
            {
                var sourceDir = ProfilerDeployDirectory / $"win-{architecture}";
                var source = sourceDir / "Datadog.Profiler.Native.dll";
                var dest = MonitoringHomeDirectory / $"win-{architecture}";
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                source = sourceDir / "Datadog.Profiler.Native.pdb";
                dest = SymbolsDirectory / $"win-{architecture}" / Path.GetFileName(source);
                CopyFile(source, dest, FileExistsPolicy.Overwrite);
            }
        });

    Target BuildProfilerSamples => _ => _
       .Description("Builds the profiler samples.")
       .Unlisted()
       .Executes(() =>
       {
           var samplesToBuild = ProfilerSamplesSolution.GetProjects("*");

           DotNetBuild(x => x
                   .SetConfiguration(BuildConfiguration)
                   .SetTargetPlatform(TargetPlatform)
                   .SetNoWarnDotNetCore3()
                   .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackageDirectory(NugetPackageDirectory))
                   .CombineWith(samplesToBuild, (c, project) => c
                       .SetProjectFile(project)));
       });

    Target BuildAndRunProfilerCpuLimitTests => _ => _
        .After(BuildProfilerSamples)
        .Description("Run the profiler container tests")
        .Requires(() => IsLinux && !IsArm64)
        .Executes(() =>
        {
            BuildAndRunProfilerIntegrationTestsInternal("(Category=CpuLimitTest)");
        });

    Target BuildAndRunProfilerIntegrationTests => _ => _
        .After(BuildProfilerSamples)
        .Description("Builds and runs the profiler integration tests")
        .Requires(() => !IsArm64)
        .Executes(() =>
        {
            // Exclude CpuLimitTest from this path: They are already launched in a specific step + specific setup
            var filter = $"{(IsLinux ? "(Category!=WindowsOnly)" : "(Category!=LinuxOnly)")}&(Category!=CpuLimitTest)";
            BuildAndRunProfilerIntegrationTestsInternal(filter);
        });

    private void BuildAndRunProfilerIntegrationTestsInternal(string filter)
    {
        var isDebugRun = IsDebugRun();

        EnsureExistingDirectory(ProfilerTestLogsDirectory);

        var integrationTestProjects = ProfilerDirectory.GlobFiles("test/*.IntegrationTests/*.csproj")
                                                        .Select(x => ProfilerSolution.GetProject(x))
                                                        .ToList();

        try
        {
            // Run these ones in parallel
            DotNetTest(config => config
                                .SetConfiguration(BuildConfiguration)
                                .SetTargetPlatform(TargetPlatform)
                                .SetDotnetPath(TargetPlatform)
                                .SetNoWarnDotNetCore3()
                                .When(TestAllPackageVersions, o => o.SetProperty("TestAllPackageVersions", "true"))
                                .When(IncludeMinorPackageVersions, o => o.SetProperty("IncludeMinorPackageVersions", "true"))
                                .EnableCrashDumps()
                                .SetFilter(filter)
                                .SetProcessLogOutput(true)
                                .SetIsDebugRun(isDebugRun)
                                .SetProcessEnvironmentVariable("DD_TESTING_OUPUT_DIR", ProfilerBuildDataDirectory)
                                .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                                .CombineWith(integrationTestProjects, (s, project) => s
                                                                                        .EnableTrxLogOutput(ProfilerBuildDataDirectory / "results" / project.Name)
                                                                                        .WithDatadogLogger()
                                                                                        .SetProjectFile(project)),
                        degreeOfParallelism: 4);
        }
        finally
        {
            CopyDumpsTo(ProfilerBuildDataDirectory);
            // A crashed occured on linux and the memory dump copy failed due a lack of permission.
            Chmod.Value.Invoke("-R 777 " + ProfilerBuildDataDirectory);
        }
    }

    Target RunClangTidyProfiler => _ => _
        .Unlisted()
        .Description("Runs Clang-tidy on native profiler")
        .DependsOn(RunClangTidyProfilerLinux)
        .DependsOn(RunClangTidyProfilerWindows);

    Target RunClangTidyProfilerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerBuildDataDirectory);

            NuGetTasks.NuGetRestore(s => s
                .SetTargetPath(ProfilerMsBuildProject)
                .SetVerbosity(NuGetVerbosity.Normal)
                .When(!string.IsNullOrEmpty(NugetPackageDirectory), o => o.SetPackagesDirectory(NugetPackageDirectory)));

            var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 };

            MSBuild(s => s
                .SetTargetPath(ProfilerMsBuildProject)
                .SetConfiguration(Configuration.Release) // This job is done only for Release
                .SetMSBuildPath()
                .SetMaxCpuCount(null)
                .DisableRestore()
                .SetTargets("BuildCpp")
                .AddProperty("RunCodeAnalysis", "true")
                .AddProperty("EnableClangTidyCodeAnalysis", "true")
                .AddProperty("ClangTidyChecks", $"\"{ClangTidyChecks}\" --list-checks")
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)
                    .SetLoggers($"FileLogger,Microsoft.Build;logfile={ProfilerBuildDataDirectory / $"windows-profiler-clang-tidy-{platform}.txt"}")));
        });

    Target RunCppCheckProfiler => _ => _
        .Unlisted()
        .Description("Runs CppCheck on native profiler")
        .DependsOn(RunCppCheckProfilerWindows)
        .DependsOn(RunCppCheckProfilerLinux);

    Target RunCppCheckProfilerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerBuildDataDirectory);

            void RunCppCheck(string projectName, MSBuildTargetPlatform platform)
            {
                var project = ProfilerSolution.GetProject(projectName);
                var cppCheckResultFile = ProfilerBuildDataDirectory / $"{project.Name}-cppcheck-{platform}";
                CppCheck.Value($"--inline-suppr  --enable=all  --project={project.Path} --xml --output-file={cppCheckResultFile}.xml  --suppressions-list={ProfilerDirectory}/cppcheck-suppressions.txt ");
            }

            foreach (var platform in new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 })
            {
                Logger.Information($"======= Run CppCheck for platform {platform}");
                RunCppCheck("Datadog.Profiler.Native", platform);
                RunCppCheck("Datadog.Profiler.Native.Windows", platform);
            }
        });

    Target CheckProfilerStaticAnalysisResults => _ => _
        .Unlisted()
        .Description("Check if static analyzers found error(s)")
        .DependsOn(CheckCppCheckResults)
        .DependsOn(CheckClangTidyResults);

    Target CheckCppCheckResults => _ => _
        .Unlisted()
        .After(RunCppCheckProfilerWindows)
        .After(RunCppCheckProfilerLinux)
        .Executes(() =>
        {
            var cppcheckResults = ProfilerBuildDataDirectory.GlobFiles(
                "*cppcheck*.xml"
            );

            if (cppcheckResults.Count == 0)
            {
                Logger.Error("::error::No CppCheck result file(s) was/were found. Did you run RunCppCheckProfiler target ?");
                throw new Exception("No CppCheck result file(s) was/were found");
            }

            foreach (var result in cppcheckResults)
            {
                Logger.Information($"Check result file {result}");
                var doc = XDocument.Load(result);
                var messages = doc.Descendants("errors").First();

                const int maxErrors = 50;

                var foundErrors = messages.Descendants("error").Where(message =>
                {
                    return string.Equals(message.Attribute("severity").Value, "error", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

                if (foundErrors.Count > 0)
                {
                    Logger.Error($"{foundErrors.Count} error(s) was/were detected by CppCheck in {Path.GetFileName(result)}");

                    foreach (var error in foundErrors.Take(maxErrors))
                    {
                        Logger.Error($"{error}");
                    }

                    if (foundErrors.Count > maxErrors)
                    {
                        Logger.Error($"... and {foundErrors.Count - maxErrors} more errors");
                    }

                    throw new Exception($"Error(s) was/were detected by CppCheck in {Path.GetFileName(result)}");
                }
            }
        });

    Target CheckClangTidyResults => _ => _
        .Unlisted()
        .After(RunClangTidyProfilerWindows)
        .After(RunClangTidyProfilerLinux)
        .Executes(async () =>
        {
            var clangTidyResults = ProfilerBuildDataDirectory.GlobFiles(
                "*clang-tidy*.txt"
            );

            if (clangTidyResults.Count == 0)
            {
                Logger.Error("::error::No Clang-Tidy result file(s) was/were found. Did you run RunClangTidyProfiler target ?");
                throw new Exception("No Clang-Tidy result file(s) was/were found");
            }

            foreach (var result in clangTidyResults)
            {
                Logger.Information($"Check result file {result}");
                using var sr = new StreamReader(result); ;

                string line;
                var errorRegex = new Regex(" error:", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
                while ((line = await sr.ReadLineAsync()) != null)
                {
                    var matched = errorRegex.Match(line);
                    if (matched.Success)
                    {
                        Logger.Error($"::error::Error(s) was/were detected by Clang-Tidy in {Path.GetFileName(result)}");
                        throw new Exception($"Error(s) was/were detected by Clang-Tidy in {Path.GetFileName(result)}");
                    }
                }
            }
        });

    Target RunClangTidyProfilerLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(async () =>
        {
            EnsureExistingDirectory(ProfilerBuildDataDirectory);

            var (arch, ext) = GetUnixArchitectureAndExtension();
            var outputFile = ProfilerBuildDataDirectory / $"linux-profiler-clang-tidy-{arch}.txt";

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DRUN_ANALYSIS=1 -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            // to run clang tidy, we first need to build the profiler
            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target all-profiler");

            var output = RunClangTidy.Value($"-j {Environment.ProcessorCount} -checks=\"{ClangTidyChecks}\" -p {NativeBuildDirectory}", logOutput: false);

            await using var file = new StreamWriter(outputFile);

            foreach (var line in output)
            {
                await file.WriteLineAsync(line.Text);
            }
        });

    Target RunCppCheckProfilerLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerBuildDataDirectory);

            var (arch, ext) = GetUnixArchitectureAndExtension();
            var outputFile = ProfilerBuildDataDirectory / $"linux-profiler-cppcheck-{arch}.xml";

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DRUN_ANALYSIS=1 -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CppCheck.Value($"-j {Environment.ProcessorCount} --inline-suppr --enable=all --project={NativeBuildDirectory}/compile_commands.json -D__linux__ -D__x86_64__ --suppressions-list={ProfilerDirectory}/cppcheck-suppressions.txt --xml --output-file={outputFile}");
        });

    Target BuildProfilerAsanTest => _ => _
        .Unlisted()
        .Description("Compile the profiler with Clang Address sanitizer")
        .DependsOn(BuildNativeLoader)
        .DependsOn(CompileProfilerWithAsanLinux)
        .DependsOn(CompileProfilerWithAsanWindows)
        .DependsOn(BuildNativeWrapper)
        .DependsOn(PublishNativeWrapper)
        .DependsOn(PublishProfiler);

    Target RunProfilerAsanTest => _ => _
        .Unlisted()
        .Description("Compile and run the profiler with Clang Address sanitizer")
        .DependsOn(BuildProfilerAsanTest)
        .DependsOn(BuildProfilerSampleForSanitiserTests)
        .DependsOn(RunSampleWithProfilerAsan);

    Target CompileProfilerWithAsanLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Before(PublishProfiler)
        .Triggers(RunUnitTestsWithAsanLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerBuildDataDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DRUN_ASAN=1 -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target all-profiler");
        });

    Target RunUnitTestsWithAsanLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            RunProfilerUnitTests("Datadog.Profiler.Native.Tests", Configuration.Release, MSBuildTargetPlatform.x64, SanitizerKind.Asan);
        });

    Target CompileProfilerWithAsanWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .Before(PublishProfiler)
        .Triggers(RunUnitTestsWithAsanWindows)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerBuildDataDirectory);

            var testProjects = ProfilerDirectory.GlobFiles("test/**/*.vcxproj");

            NuGetTasks.NuGetRestore(s => s
                   .SetTargetPath(ProfilerMsBuildProject)
                   .SetVerbosity(NuGetVerbosity.Detailed)
                   .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                       o.SetPackagesDirectory(NugetPackageDirectory))
                   .CombineWith(testProjects, (m, testProjects) => m.SetTargetPath(testProjects)));

            var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 };

            // Can't use dotnet msbuild, as needs to use the VS version of MSBuild
            // Build native profiler assets
            MSBuild(s => s
                .SetTargetPath(ProfilerMsBuildProject)
                .SetConfiguration(Configuration.Release)
                .SetMSBuildPath()
                .DisableRestore()
                .SetMaxCpuCount(null)
                .SetTargets("Build")
                .AddProperty("EnableASAN", "true")
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target RunUnitTestsWithAsanWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            foreach (var platform in new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 })
            {
                RunProfilerUnitTests("Datadog.Profiler.Native.Tests", Configuration.Release, platform, SanitizerKind.Asan);
            }
        });

    Target RunSampleWithProfilerAsan => _ => _
        .Unlisted()
        .Requires(() => Framework)
        .OnlyWhenStatic(() => IsWin || IsLinux)
        .After(BuildProfilerSampleForSanitiserTests)
        .Triggers(CheckTestResultForProfilerWithSanitizer)
        .Executes(() =>
        {
            var platforms =
                IsWin
                ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                : new[] { MSBuildTargetPlatform.x64 };

            foreach (var platform in platforms)
            {
                RunSampleWithSanitizer(platform, SanitizerKind.Asan);
            }
        });

    Target CheckTestResultForProfilerWithSanitizer => _ => _
        .Unlisted()
        .Executes(() =>
        {
            var platforms =
                IsWin
                ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                : new[] { MSBuildTargetPlatform.x64 };

            foreach (var platform in platforms)
            {
                var baseOutputDir = ProfilerBuildDataDirectory / platform.ToString();
                var pprofsOutputDir = baseOutputDir / "pprofs";
                Logger.Information($"Check if pprofs file(s) was/were generated at {pprofsOutputDir}");

                var pprofFiles = pprofsOutputDir.GlobFiles(
                    $"*.pprof"
                );

                if (pprofFiles.Count == 0)
                {
                    Logger.Error("::error::No pprof file(s) was/were generated. Maybe the profiler is not correctly attached.");
                    throw new Exception("No pprof file(s) was/were generated.");
                }

                var logsOutputDir = baseOutputDir / "logs";
                Logger.Information($"Look for profiler log file(s) in {logsOutputDir}");

                var logFiles = logsOutputDir.GlobFiles(
                    $"DD-DotNet-Profiler-Native-*.log"
                );

                if (logFiles.Count == 0)
                {
                    Logger.Error("::error::No profiler log files was/were found. Was the profiler attached to the app?");
                    throw new Exception("No profiler log files was/were found.");
                }
            }
        });

    Target BuildProfilerUbsanTest => _ => _
        .Unlisted()
        .Description("Compile the profiler with Clang Undefined-behavior sanitizer")
        .OnlyWhenStatic(() => IsLinux)
        .DependsOn(BuildNativeLoader)
        .DependsOn(CompileProfilerWithUbsanLinux)
        .DependsOn(BuildNativeWrapper)
        .DependsOn(PublishNativeWrapper)
        .DependsOn(PublishProfiler);

    Target RunProfilerUbsanTest => _ => _
        .Unlisted()
        .Description("Compile and run the profiler with Clang Undefined-behavior sanitizer")
        .OnlyWhenStatic(() => IsLinux)
        .DependsOn(BuildProfilerUbsanTest)
        .DependsOn(BuildProfilerSampleForSanitiserTests)
        .DependsOn(RunSampleWithProfilerUbsan);


    Target CompileProfilerWithUbsanLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Before(PublishProfiler)
        .Triggers(RunUnitTestsWithUbsanLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerBuildDataDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DRUN_UBSAN=1 -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target all-profiler");
        });

    Target RunUnitTestsWithUbsanLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            RunProfilerUnitTests("Datadog.Profiler.Native.Tests", Configuration.Release, MSBuildTargetPlatform.x64, SanitizerKind.Ubsan);
        });


    Target BuildProfilerSampleForSanitiserTests => _ => _
        .Unlisted()
        .Requires(() => Framework)
        .OnlyWhenStatic(() => IsWin || IsLinux)
        .After(BuildNativeLoader)
        .After(PublishProfiler)
        .After(CompileProfilerWithAsanLinux)
        .After(CompileProfilerWithAsanWindows)
        .After(CompileProfilerWithUbsanLinux)
        .Executes(() =>
        {
            var platforms =
                IsWin
                ? new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 }
                : new[] { MSBuildTargetPlatform.x64 };

            var sampleApp = ProfilerSamplesSolution.GetProject("Samples.Computer01");

            foreach (var platform in platforms)
            {
                DotNetBuild(s => s
                                .SetFramework(Framework)
                                .SetProjectFile(sampleApp)
                                .SetConfiguration(Configuration.Release)
                                .SetNoWarnDotNetCore3()
                                .SetTargetPlatform(platform));
            }
        });

    Target RunSampleWithProfilerUbsan => _ => _
        .Unlisted()
        .Requires(() => Framework)
        .OnlyWhenStatic(() => IsLinux)
        .After(BuildNativeLoader)
        .After(PublishProfiler)
        .After(CompileProfilerWithUbsanLinux)
        .Triggers(CheckTestResultForProfilerWithSanitizer)
        .Executes(() =>
        {
            RunSampleWithSanitizer(MSBuildTargetPlatform.x64, SanitizerKind.Ubsan);
        });

    Target ValidateNativeProfilerGlibcCompatibility => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileProfilerNativeSrc)
        .Before(PublishProfilerLinux)
        .Before(ExtractDebugInfoLinux)
        .Executes(() =>
        {
            var (arch, extension) = GetUnixArchitectureAndExtension();
            var dest = ProfilerDeployDirectory / arch / $"{FileNames.NativeProfiler}.{extension}";

            // The profiler has a different minimum glibc version to the tracer.
            // The _overall_ minimum is the highest of the two, but as we don't
            // currently enable the profiler on ARM64, we take the .NET runtime's minimum
            // glibc as our actual minimum in practice. Before we can enable the profiler
            // on arm64 we must first ensure we bring this glibc version down to 2.23.
            //
            // See also the ValidateNativeTracerGlibcCompatibility Nuke task and the checks
            // in shared/src/Datadog.Trace.ClrProfiler.Native/cor_profiler.cpp#L1279
            var expectedGlibcVersion = IsArm64
                ? new Version(2, 28)
                : new Version(2, 17);

            ValidateNativeLibraryGlibcCompatibility(dest, expectedGlibcVersion);
        });
    enum SanitizerKind
    {
        None,
        Asan,
        Ubsan
    };

    void RunSampleWithSanitizer(MSBuildTargetPlatform platform, SanitizerKind sanitizer)
    {
        var sampleApp = ProfilerSamplesSolution.GetProject("Samples.Computer01");

        var envVars = new Dictionary<string, string>()
            {
                { "DD_TRACE_ENABLED", "0" }, // Disable tracer for this test
                { "DD_PROFILING_ENABLED", "1" },
                { "DD_PROFILING_EXCEPTION_ENABLED", "1" },
                { "DD_PROFILING_ALLOCATION_ENABLED", "1"},
                { "DD_PROFILING_LOCK_ENABLED","1" },
                { "DD_PROFILING_HEAP_ENABLED", "1"},
                { "DD_INTERNAL_PROFILING_DEBUG_INFO_ENABLED", "1" },
                { "DD_INTERNAL_GC_THREADS_CPUTIME_ENABLED", "1" },
            };

        if (IsLinux)
        {
            if (sanitizer is SanitizerKind.Asan)
            {
                envVars["LD_PRELOAD"] = "libasan.so.6";
                // detect_leaks set to 0 to avoid false positive since not all libs are compiled against ASAN (ex. CLR binaries)
                envVars["ASAN_OPTIONS"] = "detect_leaks=0";
            }
            else if (sanitizer is SanitizerKind.Ubsan)
            {
                envVars["LD_PRELOAD"] = "libubsan.so.1";
                envVars["UBSAN_OPTIONS"] = "print_stacktrace=1";
            }
            else if (sanitizer is SanitizerKind.None)
            {
                throw new Exception($"No sanitizer has been selected. This job must run with a sanitizer");
            }
        }

        AddContinuousProfilerEnvironmentVariables(envVars);

        var baseOutputDir = ProfilerBuildDataDirectory / platform.ToString();

        envVars["DD_INTERNAL_PROFILING_OUTPUT_DIR"] = baseOutputDir / "pprofs";
        envVars["DD_TRACE_LOG_DIRECTORY"] = baseOutputDir / "logs";

        var sampleBaseOutputDir = ProfilerOutputDirectory / "bin" / $"{Configuration.Release}-{platform}" / "profiler" / "src" / "Demos";
        var sampleAppDll = sampleBaseOutputDir / sampleApp.Name / Framework / $"{sampleApp.Name}.dll";

        DotNet($"{sampleAppDll} --scenario 1 --timeout 120", platform, environmentVariables: envVars);

        static IReadOnlyCollection<Output> DotNet(string arguments, MSBuildTargetPlatform platform, IReadOnlyDictionary<string, string> environmentVariables)
        {
            var dotnetPath = DotNetSettingsExtensions.GetDotNetPath(platform);
            using var process = ProcessTasks.StartProcess(toolPath: dotnetPath, arguments: arguments, environmentVariables: environmentVariables, customLogger: DotNetTasks.DotNetLogger);
            process.AssertZeroExitCode();
            return process.Output;

        }
    }

    void RunProfilerUnitTests(string testLibrary, Configuration configuration, MSBuildTargetPlatform platform, SanitizerKind sanitizer = SanitizerKind.None, string[] additionalEnvVars = null)
    {
        var intermediateDirPath =
            IsWin
            ? (RelativePath)$"{configuration}-{platform}" / "profiler" / "test"
            : string.Empty;

        var workingDirectory = ProfilerOutputDirectory / "bin" / intermediateDirPath / testLibrary;
        EnsureExistingDirectory(workingDirectory);

        // Nuke.Tool creates a Process and run the executable inside.
        // If not set, the process will have no environment variables.
        // Profiler code relies environment variables with special meaning (ex: %ProgramData%)
        // If those environment variables are not set, some tests will fail.
        // To make sure they do not fail, just replicate the environment variables of the current process
        // and pass them along with specific variable
        // https://devblogs.microsoft.com/oldnewthing/20200520-00/?p=103775

        Dictionary<string, string> envVars = new();
        var currentEnvVars = Environment.GetEnvironmentVariables();
        if (currentEnvVars != null)
        {
            foreach (DictionaryEntry item in currentEnvVars)
            {
                envVars[item.Key.ToString()] = item.Value.ToString();
            }
        }

        var ext = IsWin ? ".exe" : string.Empty;
        var exePath = workingDirectory / $"{testLibrary}{ext}";

        if (IsLinux)
        {
            Chmod.Value.Invoke("+x " + exePath);

            if (sanitizer is SanitizerKind.Asan)
            {
                envVars["ASAN_OPTIONS"] = "detect_leaks=1";
            }
            else if (sanitizer is SanitizerKind.Ubsan)
            {
                envVars["UBSAN_OPTIONS"] = "print_stacktrace=1";
            }
        }

        AddExtraEnvVariables(envVars, additionalEnvVars);

        var testsResultFile = ProfilerBuildDataDirectory / "tests" / $"{testLibrary}.Results.{Platform}.{configuration}.{platform}.xml";
        var testExe = ToolResolver.GetLocalTool(exePath);
        testExe($"--gtest_output=xml:{testsResultFile}", workingDirectory: workingDirectory, environmentVariables: envVars);
    }
}
