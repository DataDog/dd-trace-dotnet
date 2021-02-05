using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.SocketsHttpHandler
{
    /// <summary>
    /// HttpRequestMessage interface for ducktyping
    /// </summary>
    public interface IHttpRequestMessage
    {
        /// <summary>
        /// Gets the Http Method
        /// </summary>
        HttpMethodStruct Method { get; }

        /// <summary>
        /// Gets the request uri
        /// </summary>
        Uri RequestUri { get; }

        /// <summary>
        /// Gets the request headers
        /// </summary>
        IRequestHeaders Headers { get; }
    }
}
