using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.AspNet.Loader
{
    /// <summary>
    /// Temporary startup class to launch something from the GAC
    /// </summary>
    public static class Startup
    {
        private static bool httpModuleRegistered = false;

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
            // Only load the HttpModule once per AppDomain
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
        }
    }
}
