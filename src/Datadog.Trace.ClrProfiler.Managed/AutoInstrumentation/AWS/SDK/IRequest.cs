namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IRequest interface for ducktyping
    /// </summary>
    public interface IRequest
    {
        /// <summary>
        /// Gets the HTTP method
        /// </summary>
        string HttpMethod { get; }
    }
}
