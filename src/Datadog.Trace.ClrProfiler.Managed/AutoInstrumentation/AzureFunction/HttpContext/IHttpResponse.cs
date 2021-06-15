namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AzureFunction.HttpContext
{
    /// <summary>
    /// Represents the outgoing side of an individual HTTP request.
    /// </summary>
    public interface IHttpResponse
    {
        /// <summary>
        /// Gets or sets the value for the <c>Content-Length</c> response header.
        /// </summary>
        long? ContentLength { get; set; }

        /// <summary>
        /// Gets or sets the value for the <c>Content-Type</c> response header.
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// Gets a value indicating whether response headers have been sent to the client.
        /// </summary>
        bool HasStarted { get; }

        /// <summary>Gets the response headers.</summary>
        /// public abstract IHeaderDictionary Headers { get; }

        /// <summary>
        /// Gets the <see cref="P:Microsoft.AspNetCore.Http.HttpResponse.HttpContext" /> for this response.
        /// </summary>
        IHttpContext HttpContext { get; }

        /// <summary>Gets or sets the HTTP response code.</summary>
        int StatusCode { get; set; }
    }
}
