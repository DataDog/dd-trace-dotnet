using System.Web;
using Datadog.Trace.AspNet;

[assembly: PreApplicationStartMethod(typeof(HttpApplicationStartup), "Register")]

namespace Datadog.Trace.AspNet
{
    /// <summary>
    /// Helper class use to register the <see cref="TracingHttpModule"/> into the ASP.NET pipeline.
    /// </summary>
    public static class HttpApplicationStartup
    {
        /// <summary>
        /// Registers the <see cref="TracingHttpModule"/> into the ASP.NET pipeline.
        /// </summary>
        /// <remarks>This method replaces <see cref="Tracer.Instance"/> with a new <see cref="Tracer"/> instance.</remarks>
        public static void Register()
        {
            // in ASP.NET, we always want to try to use AspNetScopeManager,
            // even if the "AspNet" integration is disabled
            Tracer.Instance = new Tracer(
                settings: null,
                agentWriter: null,
                sampler: null,
                scopeManager: new AspNetScopeManager(),
                statsd: null);

            if (Tracer.Instance.Settings.IsIntegrationEnabled(TracingHttpModule.IntegrationName))
            {
                // only register http module if integration is enabled
                HttpApplication.RegisterModule(typeof(TracingHttpModule));
            }
        }
    }
}
