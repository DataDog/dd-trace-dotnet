namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// SendMessageRequest interface for ducktyping
    /// </summary>
    public interface ISendMessageRequest : IAmazonSQSRequestWithQueueUrl, IContainsMessageAttributes
    {
    }
}
