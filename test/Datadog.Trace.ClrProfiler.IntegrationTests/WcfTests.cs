#if NET461

using System.Collections.Generic;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class WcfTests : TestHelper
    {
        private const string ServiceVersion = "1.0.0";

        public WcfTests(ITestOutputHelper output)
            : base("Wcf", output)
        {
            SetServiceVersion(ServiceVersion);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void SubmitsTraces(bool enableCallTarget, bool enableInlining)
        {
            SetCallTargetSettings(enableCallTarget, enableInlining);

            Output.WriteLine("Starting WcfTests.SubmitsTraces. Starting the Samples.Wcf requires ADMIN privileges");

            var expectedSpanCount = 4;

            const string expectedOperationName = "wcf.request";
            const string expectedServiceName = "Samples.Wcf";
            HashSet<string> expectedResourceNames = new HashSet<string>()
            {
                "http://schemas.xmlsoap.org/ws/2005/02/trust/RSTR/Issue",
                "http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue",
                "http://schemas.xmlsoap.org/ws/2005/02/trust/RST/SCT",
                "WcfSample/ICalculator/Add"
            };

            int agentPort = TcpPortProvider.GetOpenPort();
            int wcfPort = 8585;

            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"WSHttpBinding Port={wcfPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.True(spans.Count >= expectedSpanCount, $"Expecting at least {expectedSpanCount} spans, only received {spans.Count}");

                foreach (var span in spans)
                {
                    // Validate server fields
                    Assert.Equal(expectedServiceName, span.Service);
                    Assert.Equal(ServiceVersion, span.Tags[Tags.Version]);
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(SpanTypes.Web, span.Type);
                    Assert.Equal(SpanKinds.Server, span.Tags[Tags.SpanKind]);

                    // Validate resource name
                    Assert.Contains(span.Resource, expectedResourceNames);

                    // Test HTTP tags
                    Assert.Equal("POST", span.Tags[Tags.HttpMethod]);
                    Assert.Equal("http://localhost:8585/WcfSample/CalculatorService", span.Tags[Tags.HttpUrl]);
                    Assert.Equal($"localhost:{wcfPort}", span.Tags[Tags.HttpRequestHeadersHost]);
                }
            }
        }
    }
}

#endif
