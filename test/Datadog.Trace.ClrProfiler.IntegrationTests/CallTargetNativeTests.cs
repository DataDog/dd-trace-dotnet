using System.IO;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class CallTargetNativeTests : TestHelper
    {
        public CallTargetNativeTests(ITestOutputHelper output)
            : base(new EnvironmentHelper("CallTargetNativeTest", typeof(TestHelper), output, samplesDirectory: Path.Combine("test", "test-applications", "instrumentation"), prependSamplesToAppName: false), output)
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        [InlineData(4, true)]
        [InlineData(5, true)]
        [InlineData(6, true)]
        [InlineData(7, true)]
        [InlineData(8, true)]
        [InlineData(9, false)]
        public void MethodArgumentsInstrumentation(int numberOfArguments, bool fastPath)
        {
            SetEnvironmentVariable("DD_CTARGET_TESTMODE", "True");
            SetEnvironmentVariable("DD_TRACE_CALLTARGET_ENABLED", "1");
            SetEnvironmentVariable("DD_CLR_ENABLE_INLINING", "1");
            SetEnvironmentVariable("DD_INTEGRATIONS", Path.Combine(EnvironmentHelper.GetSampleProjectDirectory(), "integrations.json"));
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agentPort, arguments: numberOfArguments.ToString()))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                string beginMethodString = $"ProfilerOK: BeginMethod\\({numberOfArguments}\\)";
                if (!fastPath)
                {
                    beginMethodString = $"ProfilerOK: BeginMethod\\(Array\\)";
                }

                int beginMethodCount = Regex.Matches(processResult.StandardOutput, beginMethodString).Count;
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod").Count;

                string[] typeNames = new string[]
                {
                    ".VoidMethod",
                    ".ReturnValueMethod",
                    ".ReturnReferenceMethod",
                    ".ReturnGenericMethod<string>",
                    ".ReturnGenericMethod<int>",
                    ".ReturnGenericMethod",
                };

                Assert.Equal(32, beginMethodCount);
                Assert.Equal(32, endMethodCount);
                foreach (var typeName in typeNames)
                {
                    Assert.Contains(typeName, processResult.StandardOutput);
                }
            }
        }
    }
}
