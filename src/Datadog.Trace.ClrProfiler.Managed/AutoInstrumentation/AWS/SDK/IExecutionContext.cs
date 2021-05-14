namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IExecutionContext interface for ducktyping
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        /// Gets the ResponseContext
        /// </summary>
        IResponseContext ResponseContext { get; }
    }
}
