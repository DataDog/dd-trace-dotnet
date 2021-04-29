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
            new MockTracerAgent.Span()
            {
                Name = "aws.request",
                Service = "Samples.AWS.SQS-aws-sdk",
                Resource = "SQS.CreateQueue",
                Tags = new()
                {
                    { "aws.agent", "dotnet-aws-sdk" },
                    { "aws.operation", "CreateQueue" },
                    { "aws.queue.name", "MySQSQueue" },
                    { "aws.service", "SQS" },
                    { "component", "aws-sdk" },
                    { "span.kind", "client" },
                },
                Metrics = new()
                {
                    { "_dd.top_level", 1 }
                },
                Type = SpanTypes.Http,
            },
            new MockTracerAgent.Span()
            {
                Name = "aws.request",
                Service = "Samples.AWS.SQS-aws-sdk",
                Resource = "SQS.SendMessage",
                Tags = new()
                {
                    { "aws.agent", "dotnet-aws-sdk" },
                    { "aws.operation", "SendMessage" },
                    { "aws.service", "SQS" },
                    { "component", "aws-sdk" },
                    { "span.kind", "client" },
                },
                Metrics = new()
                {
                    { "_dd.top_level", 1 }
                },
                Type = SpanTypes.Http,
            },
            new MockTracerAgent.Span()
            {
                Name = "aws.request",
                Service = "Samples.AWS.SQS-aws-sdk",
                Resource = "SQS.SendMessage",
                Tags = new()
                {
                    { "aws.agent", "dotnet-aws-sdk" },
                    { "aws.operation", "SendMessage" },
                    { "aws.service", "SQS" },
                    { "component", "aws-sdk" },
                    { "span.kind", "client" },
                },
                Metrics = new()
                {
                    { "_dd.top_level", 1 }
                },
                Type = SpanTypes.Http,
            },
            new MockTracerAgent.Span()
            {
                Name = "aws.request",
                Service = "Samples.AWS.SQS-aws-sdk",
                Resource = "SQS.DeleteQueue",
                Tags = new()
                {
                    { "aws.agent", "dotnet-aws-sdk" },
                    { "aws.operation", "DeleteQueue" },
                    { "aws.service", "SQS" },
                    { "component", "aws-sdk" },
                    { "span.kind", "client" },
                },
                Metrics = new()
                {
                    { "_dd.top_level", 1 }
                },
                Type = SpanTypes.Http,
            },
        };

        private readonly List<AwsSqsExpectation> _synchronousExpectations = new List<AwsSqsExpectation>();
        private readonly List<AwsSqsExpectation> _asynchronousExpectations = new List<AwsSqsExpectation>();
        private readonly List<AwsSqsExpectation> _expectations = new List<AwsSqsExpectation>();

        public AwsSqsTests(ITestOutputHelper output)
            : base("Aws.Sqs", output)
        {
            /*
            _synchronousExpectations.Add(CreateExpectation("CreateQueue"));
            _synchronousExpectations.Add(CreateExpectation("ListQueues"));
            _synchronousExpectations.Add(CreateExpectation("GetQueueUrl"));
            _synchronousExpectations.Add(CreateExpectation("SendMessage"));
            _synchronousExpectations.Add(CreateExpectation("SendMessage"));
            _synchronousExpectations.Add(CreateExpectation("ReceiveMessage"));
            _synchronousExpectations.Add(CreateExpectation("DeleteMessage"));
            _synchronousExpectations.Add(CreateExpectation("SendMessageBatch"));
            _synchronousExpectations.Add(CreateExpectation("SendMessageBatch"));
            _synchronousExpectations.Add(CreateExpectation("ReceiveMessage"));
            _synchronousExpectations.Add(CreateExpectation("DeleteMessageBatch"));
            _synchronousExpectations.Add(CreateExpectation("PurgeQueue"));
            _synchronousExpectations.Add(CreateExpectation("DeleteQueue"));
            */

            _asynchronousExpectations.Add(CreateExpectation(AwsExpectation.Commands.CreateQueueRequest));
            /*
            _asynchronousExpectations.Add(CreateExpectation("ListQueues"));
            _asynchronousExpectations.Add(CreateExpectation("GetQueueUrl"));
            */
            _asynchronousExpectations.Add(CreateExpectation("SendMessage"));
            _asynchronousExpectations.Add(CreateExpectation("SendMessage"));
            /*
            _asynchronousExpectations.Add(CreateExpectation("ReceiveMessage"));
            _asynchronousExpectations.Add(CreateExpectation("DeleteMessage"));
            _asynchronousExpectations.Add(CreateExpectation("SendMessageBatch"));
            _asynchronousExpectations.Add(CreateExpectation("SendMessageBatch"));
            _asynchronousExpectations.Add(CreateExpectation("ReceiveMessage"));
            _asynchronousExpectations.Add(CreateExpectation("DeleteMessageBatch"));
            _asynchronousExpectations.Add(CreateExpectation("PurgeQueue"));
            */
            _asynchronousExpectations.Add(CreateExpectation(AwsExpectation.Commands.DeleteQueueRequest));

            _expectations.AddRange(_asynchronousExpectations);
#if NETFRAMEWORK
            _expectations.AddRange(_synchronousExpectations);
#endif
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

                var spans = agent.WaitForSpans(_expectations.Count, operationName: AwsExpectation.IntegrationOperationName);
                Assert.True(spans.Count >= _expectations.Count, $"Expecting at least {_expectations.Count} spans, only received {spans.Count}");

                spans.OrderBy(s => s.Start).Should().BeEquivalentTo(_expectedSpans, options => options
                    .WithStrictOrdering()
                    .ExcludingMissingMembers()
                    .ExcludingDefaultSpanProperties()
                    .Using<Dictionary<string, string>>(ctx =>
                    {
                        ctx.Subject.Should().ContainKeys("env", "aws.requestId", "aws.queue.url");
                        ctx.Subject.ExceptKeys("env", "aws.requestId", "aws.queue.url").Should().Equal(ctx.Expectation);
                    }).When(info => info.SelectedMemberPath.EndsWith("Tags")));
            }
        }

        private static AwsSqsExpectation CreateExpectation(string awsOperation)
        {
            return new AwsSqsExpectation("Samples.AWS.SQS-aws-sdk", $"SQS.{awsOperation}")
            {
                Operation = awsOperation,
            };
        }
    }
}
