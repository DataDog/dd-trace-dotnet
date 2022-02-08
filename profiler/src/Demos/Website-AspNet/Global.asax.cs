using System;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Website_AspNet
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private SelfInvoker _selfInvoker = null;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            StartInvoker();
        }

        private void StartInvoker()
        {
            var enableInvoker = Environment.GetEnvironmentVariable("DD_APPTEST_INVOKER_ENABLED");
            if (bool.TryParse(enableInvoker, out var isEnable) && isEnable)
            {
                _selfInvoker = new SelfInvoker();
                _selfInvoker.Start();
            }
        }

        protected void Application_Stop()
        {
            if (_selfInvoker != null)
            {
                _selfInvoker.Stop();
                _selfInvoker.Dispose();
            }
        }
    }
}
