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
            // ALWAYS register the module first and when it is executed,
            // we will determine if traces should be started or not.
            //
            // In previous releases, the ScopeManager was initialized here
            // and when that is combined with DD_LOGS_INJECTION=true,
            // a SerializationException is thrown with the type
            // log4net.Util.PropertiesDictionary (and surely other
            // frameworks types too) because the MappedDiagnosticContext structures
            // initialized at this time must be copied over to the AppDomain
            // responsible for handling the web request, but it's possible these
            // structures cannot be passed across AppDomains (they may not implement MarshalObjectByRef)
            HttpApplication.RegisterModule(typeof(AspNetHttpModule));
        }
    }
}

#endif
