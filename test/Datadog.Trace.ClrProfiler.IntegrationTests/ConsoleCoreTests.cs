using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ConsoleCoreTests
    {
        [Fact]
        public void ProfilerAttached()
        {
            // get path to native profiler dll
            var platform = Environment.Is64BitProcess ? "x64" : "x86";
            string profilerDllPath = Path.GetFullPath($"Datadog.Trace.ClrProfiler.Native.{BuildParameters.Configuration}-{platform}.dll");
            Assert.True(File.Exists(profilerDllPath), $"Profiler DLL not found at {profilerDllPath}");

            // get path to sample app that the profiler will attach to
            string appPath = Path.GetFullPath(BuildParameters.CoreClr ? "Samples.ConsoleCore.dll" : "Samples.ConsoleCore.exe");
            Assert.True(File.Exists(appPath), $"Application not found at {appPath}");

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*.json").Select(Path.GetFullPath);

            string output;
            int exitCode;

            using (Process process = ProfilerHelper.StartProfiledProcess(
                appPath,
                BuildParameters.CoreClr,
                integrationPaths,
                Instrumentation.ProfilerClsid,
                profilerDllPath))
            {
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }

            Assert.True(exitCode >= 0, $"Process exited with code {exitCode}");

            string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Contains("ProfilerAttached=True", lines);
            Assert.DoesNotContain("Add(1,2)=3", lines);
        }
    }
}
