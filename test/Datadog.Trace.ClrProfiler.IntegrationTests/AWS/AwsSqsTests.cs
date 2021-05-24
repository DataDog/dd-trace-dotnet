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
            AwsSqsSpan.GetDefault("CreateQueue")
                    .WithTag("aws.queue.name", "MySyncSQSQueue"),
            AwsSqsSpan.GetDefault("CreateQueue")
                    .WithTag("aws.queue.name", "MySyncSQSQueue2"),

            // SendMessagesWithInjectedHeaders
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // SendMessagesWithoutInjectedHeaders
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // SendMessage
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("SendMessage"),

            // ReceiveMessageAndDeleteMessage
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),

            // SendMessageBatch
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),

            // ReceiveMessagesAndDeleteMessageBatch
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // DeleteQueue
            AwsSqsSpan.GetDefault("DeleteQueue"),
            AwsSqsSpan.GetDefault("DeleteQueue"),

#endif
            // CreateSqsQueueAsync
            AwsSqsSpan.GetDefault("CreateQueue")
                    .WithTag("aws.queue.name", "MyAsyncSQSQueue"),
            AwsSqsSpan.GetDefault("CreateQueue")
                    .WithTag("aws.queue.name", "MyAsyncSQSQueue2"),

            // SendMessagesWithInjectedHeadersAsync
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // SendMessagesWithoutInjectedHeadersAsync
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // SendMessageAsync
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("SendMessage"),

            // ReceiveMessageAndDeleteMessageAsync
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),

            // SendMessageBatchAsync
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),

            // ReceiveMessagesAndDeleteMessageBatchAsync
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // DeleteQueueAsync
            AwsSqsSpan.GetDefault("DeleteQueue"),
            AwsSqsSpan.GetDefault("DeleteQueue"),
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
            SetCallTargetSettings(true);

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
            public static AwsSqsSpan GetDefault(string operationName)
            {
                return new AwsSqsSpan()
                {
                    Name = "aws.request",
                    Resource = $"SQS.{operationName}",
                    Service = "Samples.AWS.SQS-aws-sdk",
                    Tags = new()
                    {
                        { "component", "aws-sdk" },
                        { "span.kind", "client" },
                        { "aws.agent", "dotnet-aws-sdk" },
                        { "aws.operation", operationName },
                        { "aws.service", "SQS" },
                        { "http.method", "POST" },
                        { "http.status_code", "200" },
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
