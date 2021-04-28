using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// ResponseMetadata interface for ducktyping
    /// </summary>
    public interface IResponseMetadata
    {
        /// <summary>
        /// Gets the ID of the request
        /// </summary>
        string RequestId { get; }

        /// <summary>
        /// Gets the metadata associated with the request
        /// </summary>
        IDictionary<string, string> Metadata { get; }
    }
}
