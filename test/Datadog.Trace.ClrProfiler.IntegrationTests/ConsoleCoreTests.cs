using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ConsoleCoreTests : TestHelper
    {
        private readonly ITestOutputHelper _output;

        public ConsoleCoreTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ProfilerAttached()
        {
            string standardOutput;
            string standardError;
            int exitCode;

            _output.WriteLine($"Platform: {GetPlatform()}");
            _output.WriteLine($"Configuration: {BuildParameters.Configuration}");
            _output.WriteLine($"TargetFramework: {BuildParameters.TargetFramework}");
            _output.WriteLine($".NET Core: {BuildParameters.CoreClr}");
            _output.WriteLine($"Application: {GetSampleDllPath("ConsoleCore")}");
            _output.WriteLine($"Profiler DLL: {GetProfilerDllPath()}");

            using (Process process = StartSample("ConsoleCore"))
            {
                standardOutput = process.StandardOutput.ReadToEnd();
                standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }

            _output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            _output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");

            Assert.True(exitCode >= 0, $"Process exited with code {exitCode}");

            dynamic output = JsonConvert.DeserializeObject(standardOutput);
            Assert.True((bool)output.ProfilerAttached);
            Assert.Equal(6, (int)output.AddResult);
        }
    }
}
