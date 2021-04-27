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
            TagShouldExist(Tags.AwsOperationName, Always);
            TagShouldExist(Tags.AwsAgentName, Always);
            TagShouldExist(Tags.AwsServiceName, Always);
            TagShouldExist(Tags.AwsRequestId, Always);

            var creatingOrDeletingQueue = new HashSet<string>
            {
                Commands.CreateQueueRequest
            };
            TagShouldExist(Tags.AwsQueueName, when: span => creatingOrDeletingQueue.Contains(GetTag(span, Tags.AwsOperationName)));

            var operationsAgainstQueue = new HashSet<string>
            {
                Commands.PurgeQueueRequest,
                Commands.SendMessageRequest,
                Commands.SendMessageBatchRequest,
                Commands.DeleteMessageRequest,
                Commands.DeleteMessageBatchRequest
            };
            TagShouldExist(Tags.AwsQueueUrl, when: span => operationsAgainstQueue.Contains(GetTag(span, Tags.AwsOperationName)));

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
