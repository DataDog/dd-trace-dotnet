using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    public class AmazonSqsTests : TestHelper
    {
        private readonly List<AmazonSqsExpectation> _expectations = new List<AmazonSqsExpectation>();

        public AmazonSqsTests(ITestOutputHelper output)
            : base("Amazon.SQS", output)
        {
            _expectations.Add(CreateExpectation("CreateQueueRequest"));
            _expectations.Add(CreateExpectation("ListQueuesRequest"));
            _expectations.Add(CreateExpectation("GetQueueUrlRequest"));
            _expectations.Add(CreateExpectation("SendMessageRequest"));
            _expectations.Add(CreateExpectation("SendMessageRequest"));
            _expectations.Add(CreateExpectation("ReceiveMessageRequest"));
            _expectations.Add(CreateExpectation("DeleteMessageRequest"));
            _expectations.Add(CreateExpectation("SendMessageBatchRequest"));
            _expectations.Add(CreateExpectation("SendMessageBatchRequest"));
            _expectations.Add(CreateExpectation("ReceiveMessageRequest"));
            _expectations.Add(CreateExpectation("DeleteMessageBatchRequest"));
            _expectations.Add(CreateExpectation("PurgeQueueRequest"));
            _expectations.Add(CreateExpectation("DeleteQueueRequest"));
        }

#if NETCOREAPP2_1

        [Theory]
        [MemberData(nameof(PackageVersions.AmazonSqs), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion)
        {
            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var expectedSpans = 13;
                var spans = agent.WaitForSpans(expectedSpans, 500, operationName: "aws.command");
                Assert.True(spans.Count >= expectedSpans, $"Expecting at least {expectedSpans} spans, only received {spans.Count}");

                SpanTestHelpers.AssertExpectationsMet(_expectations, spans.ToList());
            }
        }
#endif

        private static AmazonSqsExpectation CreateExpectation(string awsOperation)
        {
            return new AmazonSqsExpectation(serviceName: "Samples.Amazon.SQS-aws", operationName: "aws.command", type: SpanTypes.Http)
            {
                AwsOperation = awsOperation,
            };
        }
    }
}
