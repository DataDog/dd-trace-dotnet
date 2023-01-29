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

            ThrowSillyException();

        }

        private static void ThrowSillyException()
        {
            // For some insane reason, it seems the .PDB file is only copied to the "Temporary ASP.NET Files" folder
            // after the first exception gets thrown. Because SourceLink tests rely on the PDB file being present,
            // this created a dependency on the order in which we run our tests. To prevent that, we throw an exception
            // as part of application startup, to ensure that the .PDB file will consistently be present from the very first
            // test onwards.
            try
            {
                throw new ApplicationException();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
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

                if (HttpRuntime.UsingIntegratedPipeline)
                {
                    httpContext.Server.TransferRequest(errorRoute + queryString, false, "GET", null);
                }
                else
                {
                    httpContext.Response.StatusCode = 500;
                }
            }
        }
    }
}
