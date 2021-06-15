namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AzureFunction.HttpContext
{
    /// <summary>
    /// Represents the incoming side of an individual HTTP request.
    /// </summary>
    public interface IHttpRequest
    {
        /// <summary>
        /// Gets the Content-Length header.
        /// </summary>
        long? ContentLength { get; }

        /// <summary>
        /// Gets the Content-Type header.
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Gets the <see cref="P:Microsoft.AspNetCore.Http.HttpRequest.HttpContext" /> for this request.
        /// </summary>
        IHttpContext HttpContext { get; }

        /// <summary>Gets a value indicating whether the RequestScheme is https.</summary>
        bool IsHttps { get; }

        /// <summary>Gets the HTTP method.</summary>
        /// <returns>The HTTP method.</returns>
        string Method { get; }

        /// <summary>Gets the request protocol (e.g. HTTP/1.1).</summary>
        /// <returns>The request protocol.</returns>
        string Protocol { get; }

        /// <summary>Gets the HTTP request scheme.</summary>
        string Scheme { get; }
    }
}
