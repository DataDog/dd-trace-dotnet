#if NETFRAMEWORK
using System.Web;
using System.Web.Routing;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// ControllerContext interface for ducktyping
    /// </summary>
    public interface IControllerContext
    {
        /// <summary>
        /// Gets the HttpContext
        /// </summary>
        HttpContextBase HttpContext { get; }

        /// <summary>
        /// Gets the RouteData
        /// </summary>
        RouteData RouteData { get; }
    }
}
#endif
