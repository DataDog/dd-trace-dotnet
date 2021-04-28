using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// CreateQueueResponse interface for ducktyping
    /// </summary>
    public interface ICreateQueueResponse : IAmazonWebServiceResponse
    {
        /// <summary>
        /// Gets the URL of the created Amazon SQS queue
        /// </summary>
        string QueueUrl { get; }
    }
}
