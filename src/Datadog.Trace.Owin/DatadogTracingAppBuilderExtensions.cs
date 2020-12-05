using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Owin;

namespace Datadog.Trace.Owin
{
    /// <summary>
    /// Extension methods for IAppBuilder to use Datadog middleware
    /// </summary>
    public static class DatadogTracingAppBuilderExtensions
    {
        /// <summary>
        /// Adds the <see cref="DatadogTracingOwinMiddleware"/> to the IAppBuilder instance
        /// </summary>
        /// <param name="appBuilder">The IAppBuilder instance</param>
        public static void UseDatadogTracingOwinMiddleware(this IAppBuilder appBuilder)
        {
            appBuilder.Use<DatadogTracingOwinMiddleware>();
        }
    }
}
