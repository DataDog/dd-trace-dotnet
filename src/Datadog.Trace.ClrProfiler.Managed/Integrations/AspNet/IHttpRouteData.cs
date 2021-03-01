#if NETFRAMEWORK
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// IHttpRouteData interface for ducktyping
    /// </summary>
    public interface IHttpRouteData
    {
        IHttpRoute Route { get; }

        IDictionary<string, object> Values { get; }
    }
}
#endif
