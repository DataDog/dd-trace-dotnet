namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class AwsTags
    {
        public const string Region = "aws.region";

        public const string AgentName = "aws.agent";

        public const string ServiceName = "aws.service";

        public const string OperationName = "aws.operation";

        public const string S3BucketName = "aws.bucket.name";

        public const string DynamoTableName = "aws.table.name";

        public const string SqsQueueName = "aws.queue.name";

        public const string SqsQueueUrl = "aws.queue.url";

        public const string KinesisStreamName = "aws.stream.name";

        public const string RequestId = "aws.requestId";
    }
}
