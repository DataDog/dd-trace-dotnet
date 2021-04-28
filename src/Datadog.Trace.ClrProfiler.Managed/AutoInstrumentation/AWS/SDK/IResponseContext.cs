namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IResponseContext interface for ducktyping
    /// </summary>
    public interface IResponseContext
    {
        /// <summary>
        /// Gets the SDK response
        /// </summary>
        IAmazonWebServiceResponse Response { get; }
    }
}
