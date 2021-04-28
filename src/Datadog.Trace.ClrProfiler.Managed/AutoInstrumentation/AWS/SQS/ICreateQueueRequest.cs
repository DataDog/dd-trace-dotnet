using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// CreateQueueRequest interface for ducktyping
    /// </summary>
    public interface ICreateQueueRequest
    {
        /// <summary>
        /// Gets the name of the queue
        /// </summary>
        string QueueName { get; }

        /// <summary>
        /// Gets the message attributes
        /// </summary>
        Dictionary<string, string> Attributes { get; }
    }
}
