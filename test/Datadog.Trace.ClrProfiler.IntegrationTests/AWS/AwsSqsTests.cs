using System.Collections.Generic;
using System.Linq;
using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    public class AwsSqsTests : TestHelper
    {
        private static List<MockTracerAgent.Span> _expectedSpans = new()
        {
#if NETFRAMEWORK
            // CreateSqsQueue
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.CreateQueue")
                    .WithTag("aws.operation", "CreateQueue")
                    .WithTag("aws.queue.name", "MySQSQueue"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.CreateQueue")
                    .WithTag("aws.operation", "CreateQueue")
                    .WithTag("aws.queue.name", "MySQSQueue2"),

            // SendMessageAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.SendMessage")
                    .WithTag("aws.operation", "SendMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.SendMessage")
                    .WithTag("aws.operation", "SendMessage"),

            // ReceiveMessageAndDeleteMessageAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.ReceiveMessage")
                    .WithTag("aws.operation", "ReceiveMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteMessage")
                    .WithTag("aws.operation", "DeleteMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.ReceiveMessage")
                    .WithTag("aws.operation", "ReceiveMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteMessage")
                    .WithTag("aws.operation", "DeleteMessage"),

            // SendMessageBatchAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.SendMessageBatch")
                    .WithTag("aws.operation", "SendMessageBatch"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.SendMessageBatch")
                    .WithTag("aws.operation", "SendMessageBatch"),

            // ReceiveMessagesAndDeleteMessageBatchAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.ReceiveMessage")
                    .WithTag("aws.operation", "ReceiveMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteMessageBatch")
                    .WithTag("aws.operation", "DeleteMessageBatch"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.ReceiveMessage")
                    .WithTag("aws.operation", "ReceiveMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteMessageBatch")
                    .WithTag("aws.operation", "DeleteMessageBatch"),

            // DeleteQueue
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteQueue")
                    .WithTag("aws.operation", "DeleteQueue"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteQueue")
                    .WithTag("aws.operation", "DeleteQueue"),

#endif
            // CreateSqsQueueAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.CreateQueue")
                    .WithTag("aws.operation", "CreateQueue")
                    .WithTag("aws.queue.name", "MySQSQueue"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.CreateQueue")
                    .WithTag("aws.operation", "CreateQueue")
                    .WithTag("aws.queue.name", "MySQSQueue2"),

            // SendMessageAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.SendMessage")
                    .WithTag("aws.operation", "SendMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.SendMessage")
                    .WithTag("aws.operation", "SendMessage"),

            // ReceiveMessageAndDeleteMessageAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.ReceiveMessage")
                    .WithTag("aws.operation", "ReceiveMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteMessage")
                    .WithTag("aws.operation", "DeleteMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.ReceiveMessage")
                    .WithTag("aws.operation", "ReceiveMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteMessage")
                    .WithTag("aws.operation", "DeleteMessage"),

            // SendMessageBatchAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.SendMessageBatch")
                    .WithTag("aws.operation", "SendMessageBatch"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.SendMessageBatch")
                    .WithTag("aws.operation", "SendMessageBatch"),

            // ReceiveMessagesAndDeleteMessageBatchAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.ReceiveMessage")
                    .WithTag("aws.operation", "ReceiveMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteMessageBatch")
                    .WithTag("aws.operation", "DeleteMessageBatch"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.ReceiveMessage")
                    .WithTag("aws.operation", "ReceiveMessage"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteMessageBatch")
                    .WithTag("aws.operation", "DeleteMessageBatch"),

            // DeleteQueueAsync
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteQueue")
                    .WithTag("aws.operation", "DeleteQueue"),
            AwsSqsSpan.GetDefault()
                    .WithResource("SQS.DeleteQueue")
                    .WithTag("aws.operation", "DeleteQueue"),
        };

        public AwsSqsTests(ITestOutputHelper output)
            : base("Aws.Sqs", output)
        {
        }

        [Theory]
        [MemberData(nameof(PackageVersions.AwsSqs), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion)
        {
            SetCallTargetSettings(enableCallTarget: true, enableMethodInlining: true);

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(_expectedSpans.Count, operationName: AwsExpectation.IntegrationOperationName);
                spans.Should().HaveCountGreaterOrEqualTo(_expectedSpans.Count);

                spans.OrderBy(s => s.Start).Should().BeEquivalentTo(_expectedSpans, options => options
                    .WithStrictOrdering()
                    .ExcludingMissingMembers()
                    .ExcludingDefaultSpanProperties()
                    .AssertMetricsMatchExcludingKeys("_dd.tracer_kr", "_sampling_priority_v1")
                    .AssertTagsMatchAndSpecifiedTagsPresent("env", "aws.requestId", "aws.queue.url"));
            }
        }

        private class AwsSqsSpan : MockTracerAgent.Span
        {
            public static AwsSqsSpan GetDefault()
            {
                return new AwsSqsSpan()
                {
                    Name = "aws.request",
                    Service = "Samples.AWS.SQS-aws-sdk",
                    Tags = new()
                    {
                        { "aws.agent", "dotnet-aws-sdk" },
                        { "aws.service", "SQS" },
                        { "component", "aws-sdk" },
                        { "span.kind", "client" },
                    },
                    Metrics = new()
                    {
                        { "_dd.top_level", 1 }
                    },
                    Type = SpanTypes.Http,
                };
            }
        }
    }
}
