using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

// Mirrors Build.Profiler.Steps.cs's ASAN/UBSAN targets (CompileProfilerWithAsanLinux,
// RunProfilerUnitTests, RunSampleWithSanitizer, etc.) for the native tracer instead of the
// native profiler. Deliberately does not add a Thread Sanitizer (TSAN) variant or a Windows
// path -- only what was asked for (Linux ASAN/UBSAN), matching the profiler's own Linux jobs,
// which are the only ones actually enabled in CI today (its Windows ASAN job is
// `condition: false`). `SanitizerKind` is declared once in Build.Profiler.Steps.cs and reused
// here since both files are the same `partial class Build`.
partial class Build
{
    Target BuildTracerAsanTest => _ => _
        .Unlisted()
        .Description("Compile the tracer with Clang Address sanitizer")
        .DependsOn(CompileManagedLoader)
        .DependsOn(CompileTracerWithAsanLinux)
        .DependsOn(BuildNativeLoader)
        .DependsOn(BuildManagedTracerHome)
        .DependsOn(PublishNativeTracer);

    Target RunTracerAsanTest => _ => _
        .Unlisted()
        .Description("Compile and run the tracer with Clang Address sanitizer")
        .DependsOn(BuildTracerAsanTest)
        .DependsOn(BuildTracerSampleForSanitiserTests)
        .DependsOn(RunSampleWithTracerAsan);

    Target CompileTracerWithAsanLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Before(PublishNativeTracer)
        .Triggers(RunTracerUnitTestsWithAsanLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DRUN_ASAN=1 -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target all-tracer");
        });

    Target RunTracerUnitTestsWithAsanLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            RunTracerUnitTestsWithSanitizer(SanitizerKind.Asan);
        });

    Target BuildTracerUbsanTest => _ => _
        .Unlisted()
        .Description("Compile the tracer with Clang Undefined-behavior sanitizer")
        .OnlyWhenStatic(() => IsLinux)
        .DependsOn(CompileManagedLoader)
        .DependsOn(CompileTracerWithUbsanLinux)
        .DependsOn(BuildNativeLoader)
        .DependsOn(BuildManagedTracerHome)
        .DependsOn(PublishNativeTracer);

    Target RunTracerUbsanTest => _ => _
        .Unlisted()
        .Description("Compile and run the tracer with Clang Undefined-behavior sanitizer")
        .OnlyWhenStatic(() => IsLinux)
        .DependsOn(BuildTracerUbsanTest)
        .DependsOn(BuildTracerSampleForSanitiserTests)
        .DependsOn(RunSampleWithTracerUbsan);

    Target CompileTracerWithUbsanLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Before(PublishNativeTracer)
        .Triggers(RunTracerUnitTestsWithUbsanLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(NativeBuildDirectory);

            CMake.Value(
                arguments: $"-DCMAKE_CXX_COMPILER=clang++ -DCMAKE_C_COMPILER=clang -DRUN_UBSAN=1 -B {NativeBuildDirectory} -S {RootDirectory} -DCMAKE_BUILD_TYPE={BuildConfiguration}");

            CMake.Value(
                arguments: $"--build {NativeBuildDirectory} --parallel {Environment.ProcessorCount} --target all-tracer");
        });

    Target RunTracerUnitTestsWithUbsanLinux => _ => _
        .Unlisted()
        .OnlyWhenStatic(() => IsLinux)
        .Executes(() =>
        {
            RunTracerUnitTestsWithSanitizer(SanitizerKind.Ubsan);
        });

    Target BuildTracerSampleForSanitiserTests => _ => _
        .Unlisted()
        .Requires(() => Framework)
        .OnlyWhenStatic(() => IsLinux)
        .After(BuildNativeLoader)
        .After(PublishNativeTracer)
        .After(CompileTracerWithAsanLinux)
        .After(CompileTracerWithUbsanLinux)
        .Executes(() =>
        {
            // Samples.Console: the tracer-world analog of the profiler's Samples.Computer01 --
            // minimal, no Docker, no real backend, already used elsewhere (BuildDdDotnetArtifactTests,
            // Build.Steps.cs) as a generic profiler-attach smoke sample. Built/published the same
            // way that target does, minus the extra samples that aren't needed for this job.
            var sampleProject = TracerSanitizerSampleProject;

            DotnetBuild(new[] { sampleProject }, framework: Framework, noRestore: false);

            DotNetPublish(x => x
                .EnableNoRestore()
                .EnableNoBuild()
                .EnableNoDependencies()
                .SetProject(sampleProject)
                .SetConfiguration(BuildConfiguration)
                .SetFramework(Framework)
                .SetNoWarnDotNetCore3());
        });

    Target RunSampleWithTracerAsan => _ => _
        .Unlisted()
        .Requires(() => Framework)
        .OnlyWhenStatic(() => IsLinux)
        .After(BuildNativeLoader)
        .After(PublishNativeTracer)
        .After(CompileTracerWithAsanLinux)
        .After(BuildTracerSampleForSanitiserTests)
        .Triggers(CheckTestResultForTracerWithSanitizer)
        .Executes(() =>
        {
            RunSampleWithTracerSanitizer(SanitizerKind.Asan);
        });

    Target RunSampleWithTracerUbsan => _ => _
        .Unlisted()
        .Requires(() => Framework)
        .OnlyWhenStatic(() => IsLinux)
        .After(BuildNativeLoader)
        .After(PublishNativeTracer)
        .After(CompileTracerWithUbsanLinux)
        .After(BuildTracerSampleForSanitiserTests)
        .Triggers(CheckTestResultForTracerWithSanitizer)
        .Executes(() =>
        {
            RunSampleWithTracerSanitizer(SanitizerKind.Ubsan);
        });

    Target CheckTestResultForTracerWithSanitizer => _ => _
        .Unlisted()
        .Executes(() =>
        {
            // No pprof-style artifact to check here (that's profiler-specific) -- the sample run
            // itself already asserts a zero exit code, the "Profiler attached: True" readiness
            // line, and the absence of sanitizer error markers (see RunSampleWithTracerSanitizer).
            // This target exists purely so a future addition (e.g. a build_data artifact check)
            // has a natural home, mirroring CheckTestResultForProfilerWithSanitizer's shape.
        });

    // Samples.Console is referenced by direct path elsewhere in this codebase too
    // (BuildDdDotnetArtifactTests, Build.Steps.cs) rather than via Solution.GetProject -- it isn't
    // part of the main solution Nuke loads.
    AbsolutePath TracerSanitizerSampleProject =>
        TracerDirectory / "test/test-applications/integrations/Samples.Console/Samples.Console.csproj";

    void RunTracerUnitTestsWithSanitizer(SanitizerKind sanitizer)
    {
        // Mirrors RunTracerNativeTestsLinux (Build.Steps.cs) plus the sanitizer env vars from
        // RunProfilerUnitTests (Build.Profiler.Steps.cs) -- ASAN_OPTIONS=detect_leaks=1 here
        // (stricter than the sample-app run below) since this test binary is fully
        // self-contained under gtest, same rationale as the profiler's own unit test run.
        var workingDirectory = GetNativeOutputDirectory(FileNames.NativeTracerTests);
        EnsureExistingDirectory(workingDirectory);

        var exePath = workingDirectory / FileNames.NativeTracerTests;
        Chmod.Value.Invoke("+x " + exePath);

        Dictionary<string, string> envVars = new();
        var currentEnvVars = Environment.GetEnvironmentVariables();
        if (currentEnvVars != null)
        {
            foreach (System.Collections.DictionaryEntry item in currentEnvVars)
            {
                envVars[item.Key.ToString()] = item.Value.ToString();
            }
        }

        if (sanitizer is SanitizerKind.Asan)
        {
            envVars["ASAN_OPTIONS"] = "detect_leaks=1";
        }
        else if (sanitizer is SanitizerKind.Ubsan)
        {
            envVars["UBSAN_OPTIONS"] = "print_stacktrace=1";
        }

        var testsResultFile = BuildDataDirectory / "tests" / $"{FileNames.NativeTracerTests}.Results.{BuildConfiguration}.{TargetPlatform}.{sanitizer}.xml";
        var testExe = ToolResolver.GetLocalTool(exePath);

        testExe($"--gtest_output=xml:{testsResultFile}", workingDirectory: workingDirectory, environmentVariables: envVars);
    }

    void RunSampleWithTracerSanitizer(SanitizerKind sanitizer)
    {
        // AddTracerEnvironmentVariables (BuildVariables.cs) already points CORECLR_PROFILER_PATH
        // at the (unsanitized) Native Loader inside MonitoringHomeDirectory, which dlopen()s the
        // sanitizer-built Datadog.Tracer.Native.so at runtime -- LD_PRELOAD below still catches
        // everything across that dlopen boundary, same as the profiler's own sanitizer sample
        // run (see RunSampleWithSanitizer's comment on this). This mirrors what's actually
        // loaded in production more closely than pointing CORECLR_PROFILER_PATH directly at
        // Datadog.Tracer.Native.so would.
        var envVars = new Dictionary<string, string>();
        AddTracerEnvironmentVariables(envVars);

        if (sanitizer is SanitizerKind.Asan)
        {
            // See the identical arm64-vs-x64 libasan SONAME comment in RunSampleWithSanitizer
            // (Build.Profiler.Steps.cs).
            // See the identical arm64-vs-x64 libasan SONAME comment in RunSampleWithSanitizer
            // (Build.Profiler.Steps.cs).
            // See the identical arm64-vs-x64 libasan SONAME comment in RunSampleWithSanitizer
            // (Build.Profiler.Steps.cs).
            envVars["LD_PRELOAD"] = IsArm64 ? "libasan.so.5" : "libasan.so.6";
            // detect_leaks=0: not everything in the process is built against ASAN (e.g. CLR
            // binaries), same rationale as the profiler's sample run.
            // alloc_dealloc_mismatch=0: confirmed locally that the CLR's own hosting/startup path
            // (coreclr_execute_assembly and below -- entirely inside libcoreclr.so/libhostfxr.so/
            // libhostpolicy.so, nothing in dd-trace-dotnet's own frames) trips ASAN's new[]/delete
            // operator-mismatch check once LD_PRELOAD makes ASan intercept every allocation
            // process-wide, including the CLR's own (unsanitized) allocations. Same root cause as
            // the detect_leaks=0 case above -- code we don't control and didn't build isn't ASAN-
            // clean -- just a different ASAN check tripping on it. Not needed for the plain gtest
            // unit test run (RunTracerUnitTestsWithSanitizer), which never loads the CLR hosting
            // stack at all.
            envVars["ASAN_OPTIONS"] = "detect_leaks=0:alloc_dealloc_mismatch=0";
        }
        else if (sanitizer is SanitizerKind.Ubsan)
        {
            envVars["LD_PRELOAD"] = "libubsan.so.1";
            envVars["UBSAN_OPTIONS"] = "print_stacktrace=1";
        }
        else
        {
            throw new Exception("No sanitizer has been selected. This job must run with a sanitizer");
        }

        var baseOutputDir = BuildDataDirectory / "tracer-sanitizer" / sanitizer.ToString();
        envVars["DD_TRACE_LOG_DIRECTORY"] = baseOutputDir / "logs";
        envVars["DD_TRACE_DEBUG"] = "1";
        EnsureExistingDirectory(envVars["DD_TRACE_LOG_DIRECTORY"]);

        // Matches EnvironmentHelper.GetSampleApplicationOutputDirectory (Datadog.Trace.TestHelpers)
        // for a non-published-with-RID, non-package-version-pinned build: confirmed by reading
        // that method directly, then by locating the file after a real BuildTracerSampleForSanitiserTests
        // run -- NOT ArtifactsDirectory (which is RootDirectory/artifacts/output, a different,
        // Nuke-build-side convention that this particular sample output does not use).
        var sampleAppDll = RootDirectory / "artifacts" / "bin" / "Samples.Console" / $"{BuildConfiguration}_{Framework}".ToLowerInvariant() / "Samples.Console.dll";
        if (!System.IO.File.Exists(sampleAppDll))
        {
            throw new Exception($"Could not find the sample app at {sampleAppDll} -- was BuildTracerSampleForSanitiserTests run first?");
        }

        var dotnetPath = DotNetSettingsExtensions.GetDotNetPath(IsArm64 ? ARM64TargetPlatform : Nuke.Common.Tools.MSBuild.MSBuildTargetPlatform.x64);
        using var process = Nuke.Common.Tooling.ProcessTasks.StartProcess(
            toolPath: dotnetPath,
            arguments: $"{sampleAppDll} traces 1",
            environmentVariables: envVars,
            logOutput: true);
        process.WaitForExit();

        var output = process.Output.Select(o => o.Text).ToList();
        var fullOutput = string.Join(Environment.NewLine, output);

        Serilog.Log.Information($"Sample app output:{Environment.NewLine}{fullOutput}");

        if (process.ExitCode != 0)
        {
            throw new Exception($"Samples.Console exited with code {process.ExitCode} under {sanitizer} -- see log above.");
        }

        if (!output.Any(l => l.Contains("Profiler attached: True")))
        {
            throw new Exception(
                "Samples.Console did not report the profiler as attached -- the sanitizer build was never "
                + "actually exercised by this run, so a clean exit here would not mean anything.");
        }

        // ASAN/UBSAN aborting mid-run already produces a non-zero exit code (checked above), but
        // check the well-known error-report headers too, in case a caught/swallowed exception
        // ever let the process limp to a zero exit despite a sanitizer finding.
        var sanitizerErrorMarkers = new[] { "ERROR: AddressSanitizer", "ERROR: UndefinedBehaviorSanitizer", "runtime error:", "SUMMARY: UndefinedBehaviorSanitizer" };
        var hit = sanitizerErrorMarkers.FirstOrDefault(marker => fullOutput.Contains(marker));
        if (hit != null)
        {
            throw new Exception($"Samples.Console output contains a sanitizer error marker ('{hit}') despite a zero exit code -- see log above.");
        }
    }
}
