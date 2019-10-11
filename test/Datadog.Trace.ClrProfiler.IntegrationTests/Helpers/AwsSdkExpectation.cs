namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AwsSdkExpectation : SpanExpectation
    {
        public AwsSdkExpectation(string serviceName, string operationName, string type)
        : base(serviceName, operationName, type)
        {
            // Expectations for all spans of a queue client variety should go here
            RegisterTagExpectation(Tags.SpanKind, expected: SpanKinds.Client, when: Always);
        }

        public class Commands
        {
            public const string CreateQueueRequest = "CreateQueueRequest";
            public const string ListQueuesRequest = "ListQueuesRequest";
            public const string GetQueueUrlRequest = "GetQueueUrlRequest";
            public const string SendMessageRequest = "SendMessageRequest";
            public const string DeleteMessageRequest = "DeleteMessageRequest";
            public const string SendMessageBatchRequest = "SendMessageBatchRequest";
            public const string ReceiveMessageRequest = "ReceiveMessageRequest";
            public const string DeleteMessageBatchRequest = "DeleteMessageBatchRequest";
            public const string PurgeQueueRequest = "PurgeQueueRequest";
            public const string DeleteQueueRequest = "DeleteQueueRequest";
        }
    }
}
