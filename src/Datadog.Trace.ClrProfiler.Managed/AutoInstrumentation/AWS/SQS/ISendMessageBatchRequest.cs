using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// SendMessageBatchRequest interface for ducktyping
    /// </summary>
    public interface ISendMessageBatchRequest : IAmazonSQSRequestWithQueueUrl
    {
        /// <summary>
        /// Gets the message entries
        /// </summary>
        IList Entries { get; }
    }
}
