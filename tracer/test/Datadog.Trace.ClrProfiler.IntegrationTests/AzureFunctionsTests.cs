// <copyright file="AzureFunctionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AzureFunctionsTests : TestHelper
    {
        private const string ExpectedOperationName = "azure-functions.invoke";
        private const string ExpectedServiceName = "func";

        public AzureFunctionsTests(ITestOutputHelper output)
            : base("AzureFunctions.AllTriggers", @"test\test-applications\azure-functions", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces()
        {
            SetCallTargetSettings(true);

            var expectedSpanCount = 2;

            int agentPort = TcpPortProvider.GetOpenPort();
            int functionsPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunAzureFunctionAndWaitForExit(agent.Port, azureFunctionsPort: functionsPort))
            {
                processResult.ExitCode.Should().Be(0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);
                spans.Count.Should().BeGreaterOrEqualTo(expectedSpanCount, $"Expecting at least {expectedSpanCount} spans, only received {spans.Count}");

                Output.WriteLine($"spans.Count: {spans.Count}");

                foreach (var span in spans)
                {
                    Output.WriteLine(span.ToString());
                }

                foreach (var span in spans)
                {
                    // span.Name.Should().Be(ExpectedOperationName);
                    // span.Service.Should().Be(ExpectedServiceName);
                    span.Type.Should().Be(SpanTypes.Serverless);
                }
            }
        }
    }
}
#endif
