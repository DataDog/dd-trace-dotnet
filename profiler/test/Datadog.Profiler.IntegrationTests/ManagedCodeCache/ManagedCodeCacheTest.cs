// <copyright file="ManagedCodeCacheTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.ManagedCodeCache
{
    public class ManagedCodeCacheTest
    {
        private readonly ITestOutputHelper _output;

        public ManagedCodeCacheTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.TestProfiler", new[] { "net6.0", "net8.0", "net10.0" })]
        public void ShouldValidateManagedCodeCache(string appName, string framework, string appAssembly)
        {
            // Get paths
            var profilerPath = GetTestProfilerPath();
            var appPath = GetApplicationPath(appName, framework, appAssembly);
            var workingDir = Path.GetDirectoryName(appPath);

            _output.WriteLine($"[ManagedCodeCacheTest] Test profiler: {profilerPath}");
            _output.WriteLine($"[ManagedCodeCacheTest] Application: {appPath}");
            _output.WriteLine($"[ManagedCodeCacheTest] Working directory: {workingDir}");

            var validationReportPath = Path.Combine(workingDir, "validation_report.txt");
            _output.WriteLine($"[ManagedCodeCacheTest] Validation report path: {validationReportPath}");

            // Ensure we have .dll extension for dotnet to execute
            var appDllPath = appPath.EndsWith(".dll") || appPath.EndsWith(".exe")
                ? appPath
                : $"{appPath}.dll";

            // Run the test application with the test profiler attached
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"{appDllPath} --output {validationReportPath}",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            // Set environment variables to load the test profiler
            // GUID: {12345678-ABCD-1234-ABCD-123456789ABC}
            process.StartInfo.EnvironmentVariables["CORECLR_ENABLE_PROFILING"] = "1";
            process.StartInfo.EnvironmentVariables["CORECLR_PROFILER"] = "{12345678-ABCD-1234-ABCD-123456789ABC}";
            process.StartInfo.EnvironmentVariables["CORECLR_PROFILER_PATH"] = profilerPath;

            // Disable profiler features (we only want ManagedCodeCache validation)
            process.StartInfo.EnvironmentVariables["DD_PROFILING_WALLTIME_ENABLED"] = "0";
            process.StartInfo.EnvironmentVariables["DD_PROFILING_CPU_ENABLED"] = "0";
            process.StartInfo.EnvironmentVariables["DD_PROFILING_EXCEPTION_ENABLED"] = "0";
            process.StartInfo.EnvironmentVariables["DD_PROFILING_GC_ENABLED"] = "0";
            process.StartInfo.EnvironmentVariables["DD_PROFILING_CONTENTION_ENABLED"] = "0";

            // Disable trace to avoid needing tracer dependencies
            process.StartInfo.EnvironmentVariables["DD_TRACE_ENABLED"] = "0";

            // Start the process
            process.Start();

            // Capture output
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // Log output
            _output.WriteLine("[ManagedCodeCacheTest] === STDOUT ===");
            _output.WriteLine(stdout);
            if (!string.IsNullOrEmpty(stderr))
            {
                _output.WriteLine("[ManagedCodeCacheTest] === STDERR ===");
                _output.WriteLine(stderr);
            }
            _output.WriteLine($"[ManagedCodeCacheTest] Exit code: {process.ExitCode}");

            // Verify exit code
            process.ExitCode.Should().Be(0, "test application should exit successfully");

            // Check that the validation report was created
            var reportExists = File.Exists(validationReportPath);
            _output.WriteLine($"[ManagedCodeCacheTest] Validation report exists: {reportExists}");

            reportExists.Should().BeTrue("validation report should be created");

            var reportContent = File.ReadAllText(validationReportPath);
            _output.WriteLine("[ManagedCodeCacheTest] === Validation Report ===");
            _output.WriteLine(reportContent);

            // Verify the report contains expected sections
            reportContent.Should().Contain("=== ManagedCodeCache Validation Report ===");
            reportContent.Should().Contain("## Summary");
            reportContent.Should().Contain("## Invalid IP Tests");
            reportContent.Should().Contain("Result: ✓ PASSED");
        }

        private string GetApplicationPath(string appName, string framework, string appAssembly)
        {
            var configurationAndPlatform = $"{EnvironmentHelper.GetConfiguration()}-{EnvironmentHelper.GetPlatform()}";
            var binPath = EnvironmentHelper.GetBinOutputPath();
            var appFolder = Path.Combine(binPath, configurationAndPlatform, "profiler", "src", "Demos", appName, framework);

            var extension = EnvironmentHelper.IsRunningOnWindows() ? ".exe" : string.Empty;
            return Path.Combine(appFolder, $"{appAssembly}{extension}");
        }

        private string GetTestProfilerPath()
        {
            // The test profiler is built to profiler/_build/DDProf-Test/{platform}/
            // This aligns with production profiler: profiler/_build/DDProf-Deploy/{platform}/
            // EnvironmentHelper.GetBinOutputPath() returns profiler/_build/bin
            var buildDir = Path.GetDirectoryName(EnvironmentHelper.GetBinOutputPath()); // profiler/_build
            var platform = EnvironmentHelper.IsRunningOnWindows()
                ? EnvironmentHelper.GetPlatform() // e.g., "x64"
                : $"linux-{(Environment.Is64BitProcess ? "x64" : "x86")}";

            var basePath = Path.Combine(buildDir, "DDProf-Test", platform);

            string profilerPath;
            if (EnvironmentHelper.IsRunningOnWindows())
            {
                profilerPath = Path.Combine(basePath, "Datadog.TestProfiler.dll");
            }
            else
            {
                profilerPath = Path.Combine(basePath, "Datadog.TestProfiler.so");
            }

            if (!File.Exists(profilerPath))
            {
                throw new FileNotFoundException(
                    $"Test profiler not found at {profilerPath}. " +
                    $"Build it with: cmake --build obj --target Datadog.TestProfiler -j");
            }

            return profilerPath;
        }
    }
}
