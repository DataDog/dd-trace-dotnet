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

partial class Build
{
    const string ClangTidyChecks = "-clang-analyzer-osx*,-clang-analyzer-optin.osx*,-cppcoreguidelines-avoid-magic-numbers,-cppcoreguidelines-pro-type-vararg,-readability-braces-around-statements";

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
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target all-profiler");

            if (IsAlpine)
            {
                // On Alpine, we do not have permission to access the file libunwind-prefix/src/libunwind/config/config.guess
                // Make the whole folder and its content accessible by everyone to make sure the upload process does not fail
                Chmod.Value.Invoke(" -R 777 " + NativeBuildDirectory);
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
            var (arch, ext) = GetUnixArchitectureAndExtension();
            var sourceDir = ProfilerOutputDirectory / "DDProf-Deploy" / arch;
            EnsureExistingDirectory(MonitoringHomeDirectory / arch);
            EnsureExistingDirectory(SymbolsDirectory / arch);

            var files = new[] { "Datadog.Profiler.Native.so", "Datadog.Linux.ApiWrapper.x64.so" };
            foreach (var file in files)
            {
                var source = sourceDir / file;
                var dest = MonitoringHomeDirectory / arch / file;
                CopyFile(source, dest, FileExistsPolicy.Overwrite);
            }
        });

    Target PublishProfilerWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .After(CompileProfilerNativeSrc)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatform)
            {
                var sourceDir = ProfilerOutputDirectory / "DDProf-Deploy" / $"win-{architecture}";
                var source = sourceDir / "Datadog.Profiler.Native.dll";
                var dest = MonitoringHomeDirectory / $"win-{architecture}";
                CopyFileToDirectory(source, dest, FileExistsPolicy.Overwrite);

                source = sourceDir / "Datadog.Profiler.Native.pdb";
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
                    // .SetTargetPlatform(MSBuildTargetPlatform.x64)
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

    Target RunProfilerCpuLimitTests => _ => _
        .After(CompileProfilerSamplesLinux)
        .After(CompileProfilerLinuxIntegrationTests)
        .Description("Run the profiler container tests")
        .Requires(() => IsLinux && !IsArm64)
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerTestLogsDirectory);

            var integrationTestProjects = ProfilerDirectory.GlobFiles("test/*.IntegrationTests/*.csproj")
                                                        .Select(x => ProfilerSolution.GetProject(x))
                                                        .ToList();

            try
            {
                // Always x64
                DotNetTest(config => config
                                    .SetConfiguration(BuildConfiguration)
                                    .SetTargetPlatform(MSBuildTargetPlatform.x64)
                                    .EnableNoRestore()
                                    .EnableNoBuild()
                                    .SetFilter("(Category=CpuLimitTest)")
                                    .SetProcessEnvironmentVariable("DD_TESTING_OUPUT_DIR", ProfilerBuildDataDirectory)
                                    .SetProcessEnvironmentVariable("MonitoringHomeDirectory", MonitoringHomeDirectory)
                                    .CombineWith(integrationTestProjects, (s, project) => s
                                                                                        .EnableTrxLogOutput(ProfilerBuildDataDirectory / "results" / project.Name)
                                                                                        .SetProjectFile(project)));
            }
            finally
            {
                CopyDumpsToBuildData();
            }
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
                                    .SetFilter("(Category!=WindowsOnly)")
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
                   .SetVerbosity(NuGetVerbosity.Detailed)
                   .When(!string.IsNullOrEmpty(NugetPackageDirectory), o =>
                       o.SetPackagesDirectory(NugetPackageDirectory)));

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
                .AddProperty("ClangTidyChecks", $"\"{ClangTidyChecks}\"")
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
                CppCheck.Value($"--enable=all  --project={project.Path} --xml --output-file={cppCheckResultFile}.xml");
            }

            foreach (var platform in new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86 })
            {
                Logger.Info($"======= Run CppCheck for platform {platform}");
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
                Logger.Info($"Check result file {result}");
                var doc = XDocument.Load(result);
                var messages = doc.Descendants("errors").First();

                var foundError = messages.Descendants("error").Where(message =>
                {
                    return string.Equals(message.Attribute("severity").Value, "error", StringComparison.OrdinalIgnoreCase);
                }).Any();

                if (foundError)
                {
                    Logger.Error($"::error::Error(s) was/were detected by CppCheck in {Path.GetFileName(result)}");
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
                Logger.Info($"Check result file {result}");
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
        .Executes(() =>
        {
            EnsureExistingDirectory(ProfilerBuildDataDirectory);

            var (arch, ext) = GetUnixArchitectureAndExtension();
            var outputFile = ProfilerBuildDataDirectory / $"linux-profiler-clang-tidy-{arch}.txt";

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DRUN_ANALYSIS=1 -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            // to run clang tidy, we first need to build the profiler
            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target all-profiler");

            RunClangTidy.Value($"-j {Environment.ProcessorCount} -checks=\"{ClangTidyChecks}\" -p {NativeBuildDirectory}", logFile:outputFile, logOutput: false);
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

            CppCheck.Value($"-j {Environment.ProcessorCount} --enable=all --project={NativeBuildDirectory}/compile_commands.json -D__linux__ -D__x86_64__ --suppressions-list={ProfilerDirectory}/cppcheck-suppressions.txt --xml --output-file={outputFile}");
        });
}
