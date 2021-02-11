#if NETFRAMEWORK
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// HttpControllerContext interface for ducktyping
    /// </summary>
    public interface IHttpControllerContext
    {
        IHttpRequestMessage Request { get; }

        IHttpRouteData RouteData { get; }
    }
}
#endif
