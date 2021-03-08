#if NETFRAMEWORK
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// IHttpRoute proxy for ducktyping
    /// </summary>
    public interface IHttpRoute
    {
        string RouteTemplate { get; }
    }
}
#endif
