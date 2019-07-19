#if !NETSTANDARD2_0

using System.Web;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    ///     Used as the target of a PreApplicationStartMethodAttribute on the assembly to load the AspNetHttpModule into the pipeline
    /// </summary>
    public static class AspNetStartup
    {
        /// <summary>
        ///     Registers the AspNetHttpModule at ASP.NET startup into the pipeline
        /// </summary>
        public static void Register()
        {
            // Tracer.Instance.RegisterScopeAccess(new AspNetActiveScopeAccess());

            if (Tracer.Instance.Settings.IsIntegrationEnabled(AspNetHttpModule.IntegrationName))
            {
                // only register http module if integration is enabled
                HttpApplication.RegisterModule(typeof(AspNetHttpModule));
            }
        }
    }
}

#endif
