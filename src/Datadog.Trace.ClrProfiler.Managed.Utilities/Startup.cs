using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
#if NETFRAMEWORK
using System.Web;
#endif

namespace Datadog.Trace.ClrProfiler.Managed.Utilities
{
    /// <summary>
    /// Temporary startup class to launch something from the GAC
    /// </summary>
    public static class Startup
    {
#if NETFRAMEWORK
        private static bool httpModuleRegistered = false;
#endif

        /// <summary>
        /// Injects a call to load the AspNetModule
        /// </summary>
        [InterceptMethod(
            Integration = "AspNet",
            CallerAssembly = "System.Web",
            CallerType = "System.Web.Compilation.BuildManager",
            CallerMethod = "InvokePreStartInitMethodsCore",
            TargetAssembly = "System.Web",
            TargetType = "System.Web.Compilation.BuildManager",
            TargetMethod = "InvokePreStartInitMethodsCore",
            MethodReplacementAction = MethodReplacementActionType.InsertFirst)]
        public static void TryLoadAspNetModule()
        {
#if NETFRAMEWORK
            // Do nothing. We don't expect to be running ASP.NET on .NET Core
            if (httpModuleRegistered)
            {
                return;
            }

            try
            {
                var assembly = Assembly.Load("Datadog.Trace.AspNet, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb");

                if (assembly != null)
                {
                    var type = assembly.GetType("Datadog.Trace.AspNet.TracingHttpModule");
                    HttpApplication.RegisterModule(type);
                    httpModuleRegistered = true;
                }
            }
            catch
            {
                // Do nothing I guess
            }
#else
            // Do nothing
#endif
        }
    }
}
