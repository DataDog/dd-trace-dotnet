// <copyright file="AwsSqsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
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
            // Method: CreateSqsQueue
            AwsSqsSpan.GetDefault("CreateQueue")
                    .WithTag("aws.queue.name", "MySyncSQSQueue"),
            AwsSqsSpan.GetDefault("CreateQueue")
                    .WithTag("aws.queue.name", "MySyncSQSQueue2"),

            // Method: SendMessagesWithInjectedHeaders
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // Method: SendMessagesWithoutInjectedHeaders
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // Method: SendMessage
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("SendMessage"),

            // Method: ReceiveMessageAndDeleteMessage
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),

            // Method: SendMessageBatch
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),

            // Method: ReceiveMessagesAndDeleteMessageBatch
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // Method: DeleteQueue
            AwsSqsSpan.GetDefault("DeleteQueue"),
            AwsSqsSpan.GetDefault("DeleteQueue"),

#endif
            // Note: Resource names will match the SQS API, which does not have Async suffixes
            // Method: CreateSqsQueueAsync
            AwsSqsSpan.GetDefault("CreateQueue")
                    .WithTag("aws.queue.name", "MyAsyncSQSQueue"),
            AwsSqsSpan.GetDefault("CreateQueue")
                    .WithTag("aws.queue.name", "MyAsyncSQSQueue2"),

            // Method: SendMessagesWithInjectedHeadersAsync
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // Method: SendMessagesWithoutInjectedHeadersAsync
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // Method: SendMessageAsync
            AwsSqsSpan.GetDefault("SendMessage"),
            AwsSqsSpan.GetDefault("SendMessage"),

            // Method: ReceiveMessageAndDeleteMessageAsync
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessage"),

            // Method: SendMessageBatchAsync
            AwsSqsSpan.GetDefault("SendMessageBatch"),
            AwsSqsSpan.GetDefault("SendMessageBatch"),

            // Method: ReceiveMessagesAndDeleteMessageBatchAsync
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),
            AwsSqsSpan.GetDefault("ReceiveMessage"),
            AwsSqsSpan.GetDefault("DeleteMessageBatch"),

            // Method: DeleteQueueAsync
            AwsSqsSpan.GetDefault("DeleteQueue"),
            AwsSqsSpan.GetDefault("DeleteQueue"),
        };

        public AwsSqsTests(ITestOutputHelper output)
            : base("AWS.SQS", output)
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

                var spans = agent.WaitForSpans(_expectedSpans.Count, operationName: "sqs.request");
                spans.Should().HaveCountGreaterOrEqualTo(_expectedSpans.Count);

                spans.OrderBy(s => s.Start).Should().BeEquivalentTo(_expectedSpans, options => options
                    .WithStrictOrdering()
                    .ExcludingMissingMembers()
                    .ExcludingDefaultSpanProperties()
                    .AssertMetricsMatchExcludingKeys("_dd.tracer_kr", "_sampling_priority_v1")
                    .AssertTagsMatchAndSpecifiedTagsPresent("env", "aws.requestId", "aws.queue.url", "runtime-id"));
            }
        }

        private class AwsSqsSpan : MockTracerAgent.Span
        {
            public static AwsSqsSpan GetDefault(string operationName)
            {
                return new AwsSqsSpan()
                {
                    Name = "sqs.request",
                    Resource = $"SQS.{operationName}",
                    Service = "Samples.AWS.SQS-aws-sqs",
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
