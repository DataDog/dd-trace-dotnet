#if NETFRAMEWORK
using System.Reflection;
using System.Threading;
using System.Web;
using Datadog.Trace.AspNet;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// The ASP.NET integration.
    /// </summary>
    public static class AspNetIntegration
    {
        /// <summary>
        /// Indicates whether we're initializing the HttpModule for the first time
        /// </summary>
        private static int _firstInitialization = 1;

        /// <summary>
        /// Injects a call to HttpApplication to register the HttpModule
        /// </summary>
        [InterceptMethod(
            Integration = "AspNet",
            CallerAssembly = "System.Web",
            CallerType = "System.Web.Compilation.BuildManager",
            CallerMethod = "InvokePreStartInitMethodsCore",
            TargetAssembly = "System.Web",
            TargetType = "System.Web.Compilation.BuildManager",
            TargetMethod = "InvokePreStartInitMethodsCore",
            TargetMinimumVersion = "4",
            TargetMaximumVersion = "4",
            MethodReplacementAction = MethodReplacementActionType.InsertFirst)]
        public static void TryLoadHttpModule()
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // The HttpModule was already registered
                return;
            }

            try
            {
                HttpApplication.RegisterModule(typeof(TracingHttpModule));
            }
            catch
            {
                // Unable to dynamically register module
                // Not sure if we can technically log yet or not, so do nothing
            }
        }
    }
}
#endif
