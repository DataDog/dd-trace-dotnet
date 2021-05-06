#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class MsmqTests : TestHelper
    {
        private const string ExpectedServiceName = "Samples.Msmq-msmq";

        public MsmqTests(ITestOutputHelper output)
            : base("Msmq", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Trait("Category", "EndToEnd")]
        [Fact]
        public void SubmitsTraces()
        {
            SetCallTargetSettings(true, true);

            var expectedSpanCount = 25;
            const int rounds = 12;
            const int expectedSendCount = rounds;
            const int expectedReceiveCount = rounds;
            const int expectedPurgeCount = 1;
            var sendCount = 0;
            var receiveCount = 0;
            var purgeCount = 0;
            var distributedParentSpans = new Dictionary<ulong, int>();
            var agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"{rounds}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(expectedSpanCount);
                Assert.True(spans.Count >= expectedSpanCount, $"Expecting at least {expectedSpanCount} spans, only received {spans.Count}");

                var msmqSpans = spans.Where(span => string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase));
                var manualSpans = spans.Where(span => !string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase));

                foreach (var span in msmqSpans)
                {
                    Assert.Equal(SpanTypes.Queue, span.Type);
                    Assert.Equal("Msmq", span.Tags[Tags.InstrumentationName]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                    Assert.Equal("msmq.command", span.Name);

                    var command = span.Tags[Tags.MsmqCommand];

                    if (string.Equals(command, "msmq.send", StringComparison.OrdinalIgnoreCase))
                    {
                        sendCount++;
                        Assert.Equal(SpanKinds.Producer, span.Tags[Tags.SpanKind]);
                    }
                    else if (string.Equals(command, "msmq.consume", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.Equal(SpanKinds.Consumer, span.Tags[Tags.SpanKind]);
                        receiveCount++;
                    }
                    else if (string.Equals(command, "msmq.peek", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.Equal(SpanKinds.Consumer, span.Tags[Tags.SpanKind]);
                        receiveCount++;
                    }
                    else if (string.Equals(command, "msmq.purge", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.Equal(SpanKinds.Producer, span.Tags[Tags.SpanKind]);
                        purgeCount++;
                    }
                    else
                    {
                        throw new Xunit.Sdk.XunitException($"msmq.command {command} not recognized.");
                    }
                }

                Assert.Equal(expectedSendCount, sendCount);
                Assert.Equal(expectedPurgeCount, purgeCount);
                Assert.Equal(expectedReceiveCount, receiveCount);

                foreach (var span in manualSpans)
                {
                    Assert.Equal("Samples.Msmq", span.Service);
                    Assert.Equal("1.0.0", span.Tags[Tags.Version]);
                }
            }
        }
    }
}
#endif
