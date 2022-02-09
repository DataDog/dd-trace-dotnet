using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Samples.AspNetMvc4
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var httpContext = HttpContext.Current;
            var transferRequested = httpContext.Request.QueryString["TransferRequest"].Equals("true", StringComparison.OrdinalIgnoreCase);

            if (transferRequested)
            {
                var errorRoute = "~/Error/Index";
                var errorId = Guid.NewGuid().ToString();

                var exception = httpContext.Server.GetLastError();
                System.Diagnostics.Debug.WriteLine(exception);

                httpContext.Server.ClearError();
                string queryString = $"?errorId={errorId}";
                if (httpContext.Items["ErrorStatusCode"] is int statusCode)
                {
                    queryString += $"&ErrorStatusCode={statusCode}";
                }
                httpContext.Server.TransferRequest(errorRoute + queryString, false, "GET", null);
            }
        }
    }
}
