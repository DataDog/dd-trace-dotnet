using System;
using System.Web;
using System.Web.Routing;
using global::Ninject;
using global::Ninject.Web.Common;

namespace Samples.WebForms.Ninject
{
    public class Global : HttpApplication
    {
        /// <summary>
        /// Gets the Ninject kernel from the WebActivator bootstrapper.
        /// The kernel is initialized in NinjectWebCommon.Start() which runs
        /// via PreApplicationStartMethod BEFORE Application_Start().
        /// </summary>
        public static IKernel Kernel => new Bootstrapper().Kernel;

        private void Application_Start(object sender, EventArgs e)
        {
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}
