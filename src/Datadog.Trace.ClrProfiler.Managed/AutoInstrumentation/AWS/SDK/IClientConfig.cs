namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// IClientConfig interface for ducktyping
    /// </summary>
    public interface IClientConfig
    {
        /// <summary>
        /// Gets the region endpoint of the config
        /// </summary>
        IRegionEndpoint RegionEndpoint { get; }
    }
}
