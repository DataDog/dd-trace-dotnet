#if NETFRAMEWORK

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// Http response struct copy target for ducktyping
    /// </summary>
    [DuckCopy]
    internal struct HttpResponseMessageStruct
    {
        public int StatusCode;
    }
}
#endif
