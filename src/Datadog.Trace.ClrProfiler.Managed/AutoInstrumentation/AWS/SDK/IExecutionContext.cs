namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IExecutionContext interface for ducktyping
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        /// Gets the RequestContext
        /// </summary>
        IRequestContext RequestContext { get; }

        /// <summary>
        /// Gets the ResponseContext
        /// </summary>
        IResponseContext ResponseContext { get; }
    }
}
