namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// DeleteQueueRequest interface for ducktyping
    /// </summary>
    public interface IDeleteQueueRequest
    {
        /// <summary>
        /// Gets the URL of the queue
        /// </summary>
        string QueueUrl { get; }
    }
}
