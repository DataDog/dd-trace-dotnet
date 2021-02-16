#if NETFRAMEWORK
using System.Web;
using System.Web.Routing;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// ControllerContext struct copy target for ducktyping
    /// </summary>
    [DuckCopy]
    public struct ControllerContextStruct
    {
        /// <summary>
        /// Gets the HttpContext
        /// </summary>
        public HttpContextBase HttpContext;

        /// <summary>
        /// Gets the RouteData
        /// </summary>
        public RouteData RouteData;
    }
}
#endif
