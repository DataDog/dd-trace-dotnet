using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch
{
    /// <summary>
    /// Version-agnostic interface for Elasticsearch RequestData
    /// </summary>
    public interface IRequestData
    {
        /// <summary>
        /// Gets the path of the request
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Gets the URI of the request
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// Gets the HTTP method of the request
        /// </summary>
        string Method { get; }
    }
}
