using System.Web;
using Datadog.Trace.Web;

[assembly: PreApplicationStartMethod(typeof(AspNetStartup), "Register")]

namespace Datadog.Trace.Web
{
    /// <summary>
    ///     Used as the target of a PreApplicationStartMethodAttribute on the assembly to load the TracingHttpModule into the pipeline
    /// </summary>
    public static class AspNetStartup
    {
        /// <summary>
        ///     Registers the TracingHttpModule at ASP.NET startup into the pipeline
        /// </summary>
        public static void Register()
        {
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
