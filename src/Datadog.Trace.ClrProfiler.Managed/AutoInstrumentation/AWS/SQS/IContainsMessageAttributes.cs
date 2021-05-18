using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// MessageAttributes interface for ducktyping
    /// </summary>
    public interface IContainsMessageAttributes
    {
        /// <summary>
        /// Gets or sets the message attributes
        /// </summary>
        IDictionary MessageAttributes { get; set;  }
    }
}
