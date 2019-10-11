using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AmazonSqsExpectation : AwsSdkExpectation
    {
        public AmazonSqsExpectation(string serviceName, string operationName, string type)
        : base(serviceName, operationName, type)
        {
            TagShouldExist(AwsSdkTags.OperationName, Always);
            TagShouldExist(AwsSdkTags.AgentName, Always);
            TagShouldExist(AwsSdkTags.ServiceName, Always);
            TagShouldExist(AwsSdkTags.RequestId, Always);

            var creatingOrDeletingQueue = new HashSet<string> { Commands.CreateQueueRequest };
            TagShouldExist(AwsSdkTags.SqsQueueName, when: span => creatingOrDeletingQueue.Contains(GetTag(span, AwsSdkTags.OperationName)));

            var operationsAgainstQueue = new HashSet<string>
            {
                Commands.PurgeQueueRequest,
                Commands.SendMessageRequest,
                Commands.SendMessageBatchRequest,
                Commands.DeleteMessageRequest,
                Commands.DeleteMessageBatchRequest
            };
            TagShouldExist(AwsSdkTags.SqsQueueUrl, when: span => operationsAgainstQueue.Contains(GetTag(span, AwsSdkTags.OperationName)));

            IsTopLevel = false;
        }

        public string AwsOperation { get; set; }

        public override bool ShouldInspect(MockTracerAgent.Span span)
        {
            return
                GetTag(span, AwsSdkTags.OperationName) == AwsOperation
             && base.ShouldInspect(span);
        }

        public override string Detail()
        {
            return base.Detail() + $" aws.operation={AwsOperation}";
        }
    }
}
