#if NETFRAMEWORK
using System;
using System.Linq;
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
        public void SubmitTraces()
        {
            SetCallTargetSettings(true);

            const int expectedTransactionalTraces = 12;
            const int expectedNonTransactionalTracesTraces = 12;
            const int totalTransactions = expectedTransactionalTraces + expectedNonTransactionalTracesTraces;
            const int expectedPurgeCount = 2;
            const int expectedPeekCount = 2;
            const int expectedSendCount = 10;
            const int expectedReceiveCount = 10;

            var sendCount = 0;
            var peekCount = 0;
            var receiveCount = 0;
            var purgeCount = 0;
            var transactionalTraces = 0;
            var nonTransactionalTraces = 0;

            var agentPort = TcpPortProvider.GetOpenPort();
            using var agent = new MockTracerAgent(agentPort);
            using var processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"5 5");
            Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

            var spans = agent.WaitForSpans(totalTransactions);
            Assert.True(spans.Count >= totalTransactions, $"Expecting at least {totalTransactions} spans, only received {spans.Count}");
            var msmqSpans = spans.Where(span => string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase));
            foreach (var span in msmqSpans)
            {
                Assert.Equal(SpanTypes.Queue, span.Type);
                Assert.Equal(span.Service, ExpectedServiceName, true);
                Assert.Equal("Msmq", span.Tags[Tags.InstrumentationName]);
                if (span.Tags[Tags.MsmqIsTransactionalQueue] == "True")
                {
                    Assert.Equal("Private$\\private-transactional-queue", span.Tags[Tags.MsmqQueue]);
                    transactionalTraces++;
                }
                else
                {
                    Assert.Equal("Private$\\private-nontransactional-queue", span.Tags[Tags.MsmqQueue]);
                    nonTransactionalTraces++;
                }

                Assert.NotNull(span.Tags[Tags.MsmqQueueLabel]);
                Assert.NotNull(span.Tags[Tags.MsmqQueueLastModifiedTime]);
                Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                Assert.Equal("msmq.command", span.Name);

                var command = span.Tags[Tags.MsmqCommand];

                if (string.Equals(command, "msmq.send", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal(SpanKinds.Producer, span.Tags[Tags.SpanKind]);
                    sendCount++;
                }
                else if (string.Equals(command, "msmq.consume", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal(SpanKinds.Consumer, span.Tags[Tags.SpanKind]);
                    receiveCount++;
                }
                else if (string.Equals(command, "msmq.peek", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal(SpanKinds.Consumer, span.Tags[Tags.SpanKind]);
                    peekCount++;
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

            Assert.Equal(expectedNonTransactionalTracesTraces, nonTransactionalTraces);
            Assert.Equal(expectedTransactionalTraces, transactionalTraces);
            Assert.Equal(expectedSendCount, sendCount);
            Assert.Equal(expectedPurgeCount, purgeCount);
            Assert.Equal(expectedReceiveCount, receiveCount);
            Assert.Equal(expectedPeekCount, peekCount);
        }
    }
}
#endif
