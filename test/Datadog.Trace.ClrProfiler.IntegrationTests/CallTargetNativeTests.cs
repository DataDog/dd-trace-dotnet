using System.Collections.Generic;
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

        public static IEnumerable<object[]> MethodArgumentsData()
        {
            for (int i = 0; i < 10; i++)
            {
                bool fastPath = i < 9;
                yield return new object[] { i, fastPath, false };
                yield return new object[] { i, fastPath, true };
            }
        }

        [Theory]
        [MemberData(nameof(MethodArgumentsData))]
        public void MethodArgumentsInstrumentation(int numberOfArguments, bool fastPath, bool inlining)
        {
            SetCallTargetSettings(true, inlining);
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
                int endMethodCount = Regex.Matches(processResult.StandardOutput, "ProfilerOK: EndMethod\\(").Count;

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
