using System;
using System.Reflection;
using System.Web;
using System.Web.Routing;
using Ninject;

namespace Samples.WebForms.Ninject
{
    public class Global : HttpApplication
    {
        private static IKernel _kernel;

        public static IKernel Kernel => _kernel;

        private void Application_Start(object sender, EventArgs e)
        {
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Initialize Ninject kernel with module loading from the executing assembly.
            // This triggers Ninject's AssemblyNameRetriever, which creates a temporary
            // AppDomain named "NinjectModuleLoader" to scan assemblies for modules.
            // The Datadog .NET tracer intentionally skips registering its startup hook
            // in that short-lived AppDomain. This test verifies that the tracer still
            // instruments requests in the main AppDomain after Ninject finishes loading.
            _kernel = new StandardKernel();
            _kernel.Load(Assembly.GetExecutingAssembly());
        }
    }
}
