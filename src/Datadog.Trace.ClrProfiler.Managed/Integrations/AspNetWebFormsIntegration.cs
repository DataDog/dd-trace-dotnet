#if !NETSTANDARD2_0

using System;
using System.Web;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    ///     AspNetWebFormsIntegration used to inject AspNetHttpModule IHttpModule into the application pipeline on startup
    /// </summary>
    public static class AspNetWebFormsIntegration
    {
        private static readonly AspNetHttpModule AspNetHttpModule = new AspNetWebFormsHttpModule();
        private static readonly ILog Log = LogProvider.GetLogger(typeof(AspNetWebFormsIntegration));

        /// <summary>
        ///     Calls the underlying Init() For an HttpApplication and traces the request.
        /// </summary>
        /// <param name="thisObj">The HttpApplication instance ref.</param>
        [InterceptMethod(
            TargetAssembly = "System.Web",
            TargetType = "System.Web.HttpApplication")]
        public static void Init(object thisObj)
        {
            try
            {
                var initMethodAction = DynamicMethodBuilder<Action<object>>.GetOrCreateMethodCallDelegate(thisObj.GetType(), "Init");

                initMethodAction(thisObj);

                if (!(thisObj is HttpApplication httpApplication))
                {
                    return;
                }

                // Register the IHttpModule
                AspNetHttpModule.Init(httpApplication);
            }
            catch (Exception ex)
            {
                Log.ErrorException("AspNetWebFormsIntegration Init exception - APM data not being captured", ex);
            }
        }
    }
}

#endif
