using System;
using System.Diagnostics;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class MultiThreadedSmokeTest
    {
        private readonly ITestOutputHelper _output;
        private readonly EnvironmentHelper _environmentHelper;

        public MultiThreadedSmokeTest(ITestOutputHelper output)
        {
            _output = output;
            _environmentHelper = new EnvironmentHelper(
                "DataDogThreadTest",
                typeof(MultiThreadedSmokeTest),
                output,
                samplesDirectory: "reproductions",
                prependSamplesToAppName: false);
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            if (!EnvironmentHelper.IsCoreClr())
            {
                _output.WriteLine("Ignored for .NET Framework");
                return;
            }

            var applicationPath = _environmentHelper.GetSampleApplicationPath().Replace(@"\\", @"\");
            var executable = _environmentHelper.GetSampleExecutionSource();

            var startInfo =
                EnvironmentHelper.IsCoreClr()
                    ? new ProcessStartInfo(executable, $"{applicationPath}")
                    : new ProcessStartInfo(executable);

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = false;

            var result = Process.Start(startInfo);

            if (result == null)
            {
                throw new NullException("We need a reference to the process for this test.");
            }

            var reasonableWaitTime = 30_000;

            var ranToCompletion = result.WaitForExit(reasonableWaitTime);

            if (!ranToCompletion)
            {
                throw new TimeoutException("The smoke test is running for too long or was lost.");
            }

            var successCode = 0;

            Assert.True(successCode == result.ExitCode, "Non-success exit code: where there is smoke, there is fire.");
        }
    }
}
