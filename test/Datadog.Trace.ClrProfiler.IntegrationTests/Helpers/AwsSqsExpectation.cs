using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Integrations.AWS;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public class AwsSqsExpectation : AwsExpectation
    {
        private const string ExpectedQueueName = "MySQSQueue";

        public AwsSqsExpectation(string serviceName, string resourceName)
        : base(serviceName, resourceName)
        {
            TagShouldExist(Tags.AwsOperationName, Always);
            TagShouldExist(Tags.AwsAgentName, Always);
            TagShouldExist(Tags.AwsServiceName, Always);
            TagShouldExist(Tags.AwsRequestId, Always);

            var creatingOrDeletingQueue = new HashSet<string>
            {
                Commands.CreateQueueRequest
            };
            RegisterTagExpectation(key: Tags.AwsQueueName, expected: ExpectedQueueName, when: span => creatingOrDeletingQueue.Contains(GetTag(span, Tags.AwsOperationName)));

            var operationsAgainstQueue = new HashSet<string>
            {
                Commands.PurgeQueueRequest,
                Commands.SendMessageRequest,
                Commands.SendMessageBatchRequest,
                Commands.DeleteMessageRequest,
                Commands.DeleteMessageBatchRequest,
                Commands.DeleteQueueRequest
            };
            RegisterTagExpectation(key: Tags.AwsQueueUrl, isExpected: tag => tag.EndsWith(ExpectedQueueName), errorMessage: $"Tag QueueUrl must end with the expected queue name: {ExpectedQueueName}", when: span => operationsAgainstQueue.Contains(GetTag(span, Tags.AwsOperationName)));

            IsTopLevel = false;
        }

        public string Operation { get; set; }

        public override bool Matches(MockTracerAgent.Span span)
        {
            return
                GetTag(span, Tags.AwsOperationName) == Operation
             && base.Matches(span);
        }
    }
}
