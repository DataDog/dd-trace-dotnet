namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// DeleteMessageBatchRequest interface for ducktyping
    /// </summary>
    public interface IDeleteMessageBatchRequest
    {
        /// <summary>
        /// Gets the URL of the queue
        /// </summary>
        string QueueUrl { get; }
    }
}
