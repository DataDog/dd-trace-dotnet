// <copyright file="MsmqTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
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
        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public void SubmitTraces()
        {
            const int expectedTransactionalTraces = 13;
            const int expectedNonTransactionalTracesTraces = 12;
            const int totalTransactions = expectedTransactionalTraces + expectedNonTransactionalTracesTraces;
            const int expectedPurgeCount = 2;
            const int expectedPeekCount = 3;
            const int expectedSendCount = 10;
            const int expectedReceiveCount = 10;

            var sendCount = 0;
            var peekCount = 0;
            var receiveCount = 0;
            var purgeCount = 0;
            var transactionalTraces = 0;
            var nonTransactionalTraces = 0;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = RunSampleAndWaitForExit(agent, arguments: $"5 5");

            var spans = agent.WaitForSpans(totalTransactions);
            Assert.True(spans.Count >= totalTransactions, $"Expecting at least {totalTransactions} spans, only received {spans.Count}");
            var msmqSpans = spans.Where(span => string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase));
            foreach (var span in msmqSpans)
            {
                var result = span.IsMsmq();
                Assert.True(result.Success, result.ToString());

                span.Service.Should().Be(ExpectedServiceName);
                span.Tags.Should().Contain(new System.Collections.Generic.KeyValuePair<string, string>(Tags.InstrumentationName, "msmq"));
                if (span.Tags[Tags.MsmqIsTransactionalQueue] == "True")
                {
                    span.Tags[Tags.MsmqQueuePath].Should().Be(".\\Private$\\private-transactional-queue");
                    transactionalTraces++;
                }
                else
                {
                    span.Tags[Tags.MsmqQueuePath].Should().Be(".\\Private$\\private-nontransactional-queue");
                    nonTransactionalTraces++;
                }

                span.Tags?.ContainsKey(Tags.Version).Should().BeFalse("External service span should not have service version tag.");

                var command = span.Tags[Tags.MsmqCommand];

                if (string.Equals(command, "msmq.send", StringComparison.OrdinalIgnoreCase))
                {
                    span.Tags[Tags.MsmqMessageWithTransaction].Should().Be(span.Tags[Tags.MsmqIsTransactionalQueue], "The program is supposed to send messages within transactions to transactional queues, and outside of transactions to non transactional queues");
                    span.Tags[Tags.SpanKind].Should().Be(SpanKinds.Producer);
                    span.Resource.Should().Be($"msmq.send {span.Tags[Tags.MsmqQueuePath]}");
                    sendCount++;
                }
                else if (string.Equals(command, "msmq.receive", StringComparison.OrdinalIgnoreCase))
                {
                    span.Tags[Tags.SpanKind].Should().Be(SpanKinds.Consumer);
                    span.Resource.Should().Be($"msmq.receive {span.Tags[Tags.MsmqQueuePath]}");
                    receiveCount++;
                }
                else if (string.Equals(command, "msmq.peek", StringComparison.OrdinalIgnoreCase))
                {
                    span.Tags[Tags.SpanKind].Should().Be(SpanKinds.Consumer);
                    span.Resource.Should().Be($"msmq.peek {span.Tags[Tags.MsmqQueuePath]}");
                    peekCount++;
                }
                else if (string.Equals(command, "msmq.purge", StringComparison.OrdinalIgnoreCase))
                {
                    span.Tags[Tags.SpanKind].Should().Be(SpanKinds.Client);
                    span.Resource.Should().Be($"msmq.purge {span.Tags[Tags.MsmqQueuePath]}");
                    purgeCount++;
                }
                else
                {
                    throw new Xunit.Sdk.XunitException($"msmq.command {command} not recognized.");
                }
            }

            nonTransactionalTraces.Should().Be(expectedNonTransactionalTracesTraces);
            transactionalTraces.Should().Be(expectedTransactionalTraces);
            sendCount.Should().Be(expectedSendCount);
            purgeCount.Should().Be(expectedPurgeCount);
            receiveCount.Should().Be(expectedReceiveCount);
            peekCount.Should().Be(expectedPeekCount);
            telemetry.AssertIntegrationEnabled(IntegrationId.Msmq);
        }
    }
}
#endif
