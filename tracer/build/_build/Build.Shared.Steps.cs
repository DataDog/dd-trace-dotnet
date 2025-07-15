using System;
using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.IO;
using System.Linq;
using System.IO;
using DiffMatchPatch;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Utilities;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using Logger = Serilog.Log;

partial class Build
{
    Target CompileNativeLoader => _ => _
        .Unlisted()
        .Description("Compiles the native loader")
        .DependsOn(CompileNativeLoaderWindows)
        .DependsOn(CompileNativeLoaderLinux)
        .DependsOn(CompileNativeLoaderOsx);

    Target CompileNativeLoaderNativeTests => _ => _
        .Unlisted()
        .Description("Compiles the native loader native test")
        .DependsOn(CompileNativeLoaderTestsWindows)
        .DependsOn(CompileNativeLoaderTestsLinux);

    Target CompileNativeLoaderWindows => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            // If we're building for x64, build for x86 too
            var platforms = ArchitecturesForPlatformForTracer;

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

    Target CompileNativeLoaderTestsWindows => _ => _
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
                .SetTargetPath(NativeLoaderTestsProject)
                .SetConfiguration(BuildConfiguration)
                .SetMSBuildPath()
                .DisableRestore()
                .SetMaxCpuCount(null)
                .CombineWith(platforms, (m, platform) => m
                    .SetTargetPlatform(platform)));
        });

    Target RunNativeLoaderTestsWindows => _ => _
        .Unlisted()
        .After(CompileNativeLoaderTestsWindows)
        .OnlyWhenStatic(() => IsWin)
        .Executes(() =>
        {
            foreach (var architecture in ArchitecturesForPlatformForTracer)
            {
                var workingDirectory = NativeLoaderTestsProject.Directory / "bin" / BuildConfiguration / architecture.ToString();
                var testsResultFile = BuildDataDirectory / "tests" / $"{FileNames.NativeLoaderTests}.Results.{BuildConfiguration}.{TargetPlatform}.xml";
                var exePath = workingDirectory / $"{FileNames.NativeLoaderTests}.exe";
                var testExe = ToolResolver.GetLocalTool(exePath);
                testExe($"--gtest_output=xml:{testsResultFile}", workingDirectory: workingDirectory);
            }
        });

    Target CompileNativeLoaderLinux => _ => _
        .Unlisted()
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
                arguments: $"--build . --parallel {Environment.ProcessorCount} --target native-loader",
                workingDirectory: NativeBuildDirectory);
        });

    Target CompileNativeLoaderTestsLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");
            CMake.Value(
                arguments: $"--build . --parallel {Environment.ProcessorCount} --target {FileNames.NativeLoaderTests}",
                workingDirectory: NativeBuildDirectory);
        });

    Target RunNativeLoaderTestsLinux => _ => _
        .Unlisted()
        .After(CompileNativeLoaderTestsLinux)
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            var workingDirectory = SharedTestsDirectory / FileNames.NativeLoaderTests / "bin";
            EnsureExistingDirectory(workingDirectory);

            var exePath = workingDirectory / FileNames.NativeLoaderTests;
            Chmod.Value.Invoke("+x " + exePath);

            var testsResultFile = BuildDataDirectory / "tests" / $"{FileNames.NativeLoaderTests}.Results.{BuildConfiguration}.{TargetPlatform}.xml";

            var testExe = ToolResolver.GetLocalTool(exePath);
            testExe($"--gtest_output=xml:{testsResultFile}", workingDirectory: workingDirectory);
        });

    Target CompileNativeLoaderOsx => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsOsx)
        .Executes(() =>
        {
            DeleteDirectory(NativeLoaderProject.Directory / "bin");

            var finalArchs = FastDevLoop ? "arm64" : string.Join(';', OsxArchs);
            var buildDirectory = NativeBuildDirectory + "_" + finalArchs.Replace(';', '_');
            EnsureExistingDirectory(buildDirectory);

            var envVariables = new Dictionary<string, string> { ["CMAKE_OSX_ARCHITECTURES"] = finalArchs };

            // Build native
            CMake.Value(
                arguments: $"-B {buildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration} -DUNIVERSAL=OFF",
                environmentVariables: envVariables);
            CMake.Value(
                arguments: $"--build {buildDirectory} --parallel {Environment.ProcessorCount} --target {FileNames.NativeLoader}",
                environmentVariables: envVariables);

            var sourceFile = NativeLoaderProject.Directory / "bin" / $"{NativeLoaderProject.Name}.dylib";

            foreach (var arch in finalArchs.Split(';'))
            {
                // Check the architecture of the build
                var output = Lipo.Value(arguments: $"-archs {sourceFile}", logOutput: false);
                var strOutput = string.Join('\n', output.Where(o => o.Type == OutputType.Std).Select(o => o.Text));
                if (!strOutput.Contains(arch, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ApplicationException($"Invalid architecture, expected: '{arch}', actual: '{strOutput}'");
                }
            }
        });

    Target CppCheckNativeLoader => _ => _
        .Unlisted()
        .Description("Runs CppCheck over the native loader")
        .DependsOn(CppCheckNativeLoaderUnix);

    Target CppCheckNativeLoaderUnix => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux || IsOsx)
        .Executes(() =>
        {
            var (arch, ext) = GetUnixArchitectureAndExtension();
            CppCheck.Value(arguments: $"--inconclusive --project={NativeLoaderProject.Path} --output-file={BuildDataDirectory}/{NativeLoaderProject.Name}-cppcheck-{arch}.xml --xml --enable=all --suppress=\"noExplicitConstructor\" --suppress=\"cstyleCast\" --suppress=\"duplicateBreak\" --suppress=\"unreadVariable\" --suppress=\"functionConst\" --suppress=\"funcArgNamesDifferent\" --suppress=\"variableScope\" --suppress=\"useStlAlgorithm\" --suppress=\"functionStatic\" --suppress=\"initializerList\" --suppress=\"redundantAssignment\" --suppress=\"redundantInitialization\" --suppress=\"shadowVariable\" --suppress=\"constParameter\" --suppress=\"unusedPrivateFunction\" --suppress=\"unusedFunction\" --suppress=\"missingInclude\" --suppress=\"unmatchedSuppression\" --suppress=\"knownConditionTrueFalse\"");
            CppCheck.Value(arguments: $"--inconclusive --project={NativeLoaderProject.Path} --output-file={BuildDataDirectory}/{NativeLoaderProject.Name}-cppcheck-{arch}.txt --enable=all --suppress=\"noExplicitConstructor\" --suppress=\"cstyleCast\" --suppress=\"duplicateBreak\" --suppress=\"unreadVariable\" --suppress=\"functionConst\" --suppress=\"funcArgNamesDifferent\" --suppress=\"variableScope\" --suppress=\"useStlAlgorithm\" --suppress=\"functionStatic\" --suppress=\"initializerList\" --suppress=\"redundantAssignment\" --suppress=\"redundantInitialization\" --suppress=\"shadowVariable\" --suppress=\"constParameter\" --suppress=\"unusedPrivateFunction\" --suppress=\"unusedFunction\" --suppress=\"missingInclude\" --suppress=\"unmatchedSuppression\" --suppress=\"knownConditionTrueFalse\"");
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
            foreach (var architecture in ArchitecturesForPlatformForTracer)
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

            if (AsUniversal)
            {
                var libc = IsArm64 ? "libc.musl-aarch64.so.1" : "libc.musl-x86_64.so.1";
                PatchElf.Value.Invoke($"--remove-needed {libc} {dest / source.Name} --remove-rpath");
            }
        });

    Target PublishNativeLoaderOsx => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsOsx)
        .After(CompileNativeLoader)
        .Executes(() =>
        {
            var dest = MonitoringHomeDirectory / "osx";

            // Copy loader.conf
            CopyFileToDirectory(
                NativeLoaderProject.Directory / "bin" / "loader.conf",
                dest,
                FileExistsPolicy.Overwrite);

            // Copy the universal binary to the output folder
            CopyFileToDirectory(
                NativeLoaderProject.Directory / "bin" / $"{NativeLoaderProject.Name}.dylib",
                dest,
                FileExistsPolicy.Overwrite,
                true);
        });

    Target ValidateNativeTracerGlibcCompatibility => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .After(CompileTracerNativeSrc)
        .After(PublishNativeTracer)
        .Before(ExtractDebugInfoLinux)
        .Executes(() =>
        {
            var (arch, extension) = GetUnixArchitectureAndExtension();
            var dest = MonitoringHomeDirectory / arch / $"{NativeTracerProject.Name}.{extension}";

            // If we need to increase this version on arm64 later, that is ok as long
            // as it doesn't go above 2.23. Just update the version below. We must
            // NOT increase it beyond 2.23, or increase the version on x64.
            //
            // See also the ValidateNativeProfilerGlibcCompatibility Nuke task and the checks
            // in shared/src/Datadog.Trace.ClrProfiler.Native/cor_profiler.cpp#L1279
            var expectedGlibcVersion = IsArm64
                ? new Version(2, 18)
                : new Version(2, 17);

            ValidateNativeLibraryGlibcCompatibility(dest, expectedGlibcVersion);
        });

    void ValidateNativeLibraryGlibcCompatibility(AbsolutePath libraryPath, Version expectedGlibcVersion, IEnumerable<string> allowedSymbols = null)
    {
        var filename = Path.GetFileNameWithoutExtension(libraryPath);
        var glibcVersion = FindMaxGlibcVersion(libraryPath, allowedSymbols);

        Logger.Information("Maximum required glibc version for {Filename} is {GlibcVersion}", filename, glibcVersion);

        if (IsAlpine && glibcVersion is not null)
        {
            throw new Exception($"Alpine build of {filename} should not have glibc symbols in the binary, but found {glibcVersion}");
        }
        else if (!IsAlpine && glibcVersion != expectedGlibcVersion)
        {
            throw new Exception($"{filename} should have a maximum required glibc version of {expectedGlibcVersion} but has {glibcVersion}");
        }
    }

    Version FindMaxGlibcVersion(AbsolutePath libraryPath, IEnumerable<string> allowedSymbols)
    {
        var output = Nm.Value($"--with-symbol-versions -D {libraryPath} ").Select(x => x.Text).ToList();

        // Gives output similar to this:
        // 0000000000170498 T SetGitMetadataForApplication
        // 000000000016f944 T ThreadsCpuManager_Map
        //                  w __cxa_finalize@GLIBC_2.17
        //                  U __cxa_thread_atexit_impl@GLIBC_2.18
        //                  U __duplocale@GLIBC_2.17
        //                  U __environ@GLIBC_2.17
        //                  U __errno_location@GLIBC_2.17
        //                  U __freelocale@GLIBC_2.17
        //                  U __fxstat@GLIBC_2.17
        //                  U __fxstat64@GLIBC_2.17
        //                  U __getdelim@GLIBC_2.17
        //                  w __gmon_start__
        //                  U __iswctype_l@GLIBC_2.17
        //                  U __lxstat@GLIBC_2.17
        //                  U __newlocale@GLIBC_2.17
        //
        // We only care about the Undefined symbols that are in glibc
        // In this example, we will return 2.18

        return output
              .Where(x => x.Contains("@GLIBC_") && allowedSymbols?.Any(y => x.Contains(y)) != true)
              .Select(x => System.Version.Parse(x.Substring(x.IndexOf("@GLIBC_") + 7)))
              .Max();
    }

    void CompareNativeSymbolsSnapshot(AbsolutePath libraryPath, string snapshotNamePrefix)
    {
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
        var verifiedPath = TestsDirectory / "snapshots" / $"{snapshotNamePrefix}.verified.txt";
        var verified = File.Exists(verifiedPath)
                           ? File.ReadAllText(verifiedPath)
                           : string.Empty;

        var libraryName = Path.GetFileNameWithoutExtension(libraryPath);
        Logger.Information("Comparing snapshot of Undefined symbols in the {LibraryName} using {Path}...", libraryName, verifiedPath);

        var dmp = new diff_match_patch();
        var diff = dmp.diff_main(verified, received);
        dmp.diff_cleanupSemantic(diff);

        var changedSymbols = diff
                            .Where(x => x.operation != Operation.EQUAL)
                            .Select(x => x.text.Trim())
                            .ToList();

        if (changedSymbols.Count == 0)
        {
            Logger.Information("No changes found in Undefined symbols in {LibraryName}", libraryName);
            return;
        }

        // Print the expected values, so it's easier to copy-paste them into the snapshot file as required
        Logger.Information("Received snapshot for {LibraryName}{Break}{Symbols}", libraryName, Environment.NewLine, received);

        Logger.Information("Changed symbols for {LibraryName}:", libraryName);

        PrintDiff(diff);

        throw new Exception($"Found differences in undefined symbols in {libraryName}. {Environment.NewLine} These are shown above as both a diff and the" +
                            "full expected snapshot. Verify that these changes are expected, and will not cause problems. " +
                            "Removing symbols is generally a safe operation, but adding them could cause crashes. " +
                            $"If the new symbols are safe to add, update the snapshot file at {verifiedPath} with the " +
                            "new values");
    }
}
