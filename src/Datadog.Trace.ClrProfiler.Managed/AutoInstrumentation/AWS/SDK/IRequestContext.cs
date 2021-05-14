namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IRequestContext interface for ducktyping
    /// </summary>
    public interface IRequestContext
    {
        /// <summary>
        /// Gets the client config
        /// </summary>
        IClientConfig ClientConfig { get; }

        /// <summary>
        /// Gets the Request
        /// </summary>
        IRequest Request { get; }
    }
}
