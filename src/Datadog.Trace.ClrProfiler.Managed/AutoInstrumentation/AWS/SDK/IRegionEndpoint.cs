namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IRegionEndpoint interface for ducktyping
    /// </summary>
    public interface IRegionEndpoint
    {
        /// <summary>
        /// Gets the system name of the region endpoint
        /// </summary>
        string SystemName { get; }
    }
}
