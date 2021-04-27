using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Integrations.AWS;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public class AwsSqsExpectation : AwsExpectation
    {
        public AwsSqsExpectation(string serviceName, string resourceName)
        : base(serviceName, resourceName)
        {
            TagShouldExist(AwsTags.OperationName, Always);
            TagShouldExist(AwsTags.AgentName, Always);
            TagShouldExist(AwsTags.ServiceName, Always);
            TagShouldExist(AwsTags.RequestId, Always);

            var creatingOrDeletingQueue = new HashSet<string>
            {
                Commands.CreateQueueRequest
            };
            TagShouldExist(AwsTags.SqsQueueName, when: span => creatingOrDeletingQueue.Contains(GetTag(span, AwsTags.OperationName)));

            var operationsAgainstQueue = new HashSet<string>
            {
                Commands.PurgeQueueRequest,
                Commands.SendMessageRequest,
                Commands.SendMessageBatchRequest,
                Commands.DeleteMessageRequest,
                Commands.DeleteMessageBatchRequest
            };
            TagShouldExist(AwsTags.SqsQueueUrl, when: span => operationsAgainstQueue.Contains(GetTag(span, AwsTags.OperationName)));

            IsTopLevel = false;
        }

        public string AwsOperation { get; set; }

        public string QueueName { get; set; }

        public override bool Matches(MockTracerAgent.Span span)
        {
            return
                GetTag(span, AwsTags.OperationName) == AwsOperation
             && base.Matches(span);
        }
    }
}
