using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Http;
using System;

namespace Samples.AspNetMvc5
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
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
