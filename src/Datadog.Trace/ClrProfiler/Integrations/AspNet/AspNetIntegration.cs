// <copyright file="AspNetIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Web;
using Datadog.Trace.AspNet;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// The ASP.NET integration.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AspNetIntegration
    {
        /// <summary>
        /// Indicates whether we're initializing the HttpModule for the first time
        /// </summary>
        private static int _firstInitialization = 1;

        /// <summary>
        /// Injects a call to HttpApplication to register the HttpModule
        /// </summary>
        [InsertFirstInterceptMethod(
            Integration = "AspNet",
            CallerAssembly = "System.Web",
            CallerType = "System.Web.Compilation.BuildManager",
            CallerMethod = "InvokePreStartInitMethodsCore",
            TargetAssembly = "System.Web",
            TargetType = "System.Web.Compilation.BuildManager",
            TargetMethod = "InvokePreStartInitMethodsCore",
            TargetMinimumVersion = "4",
            TargetMaximumVersion = "4")]
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
