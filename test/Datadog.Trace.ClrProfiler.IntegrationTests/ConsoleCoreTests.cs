using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ConsoleCoreTests
    {
        private readonly ITestOutputHelper _output;

        public ConsoleCoreTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ProfilerAttached()
        {
            // get path to native profiler dll
            string profilerDllPath = Path.GetFullPath("Datadog.Trace.ClrProfiler.Native.dll");
            Assert.True(File.Exists(profilerDllPath), $"Profiler DLL not found at {profilerDllPath}");

            // get path to sample app that the profiler will attach to
            string appPath = Path.GetFullPath(BuildParameters.CoreClr ? "Samples.ConsoleCore.dll" : "Samples.ConsoleCore.exe");
            Assert.True(File.Exists(appPath), $"Application not found at {appPath}");

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*.json").Select(Path.GetFullPath);

            string standardOutput;
            string standardError;
            int exitCode;

            using (Process process = ProfilerHelper.StartProcessWithProfiler(
                appPath,
                BuildParameters.CoreClr,
                integrationPaths,
                Instrumentation.ProfilerClsid,
                profilerDllPath))
            {
                standardOutput = process.StandardOutput.ReadToEnd();
                standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                _output.WriteLine(standardError);
            }

            Assert.True(exitCode >= 0, $"Process exited with code {exitCode}");

            dynamic output = JsonConvert.DeserializeObject(standardOutput);
            Assert.True((bool)output.ProfilerAttached);
            Assert.Equal(6, (int)output.AddResult);
        }
    }
}
