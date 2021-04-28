namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// AmazonWebServiceResponse interface for ducktyping
    /// </summary>
    public interface IAmazonWebServiceResponse
    {
        /// <summary>
        /// Gets the length of the content
        /// </summary>
        long ContentLength { get; }

        /// <summary>
        /// Gets the response metadata
        /// </summary>
        IResponseMetadata ResponseMetadata { get; }
    }
}
