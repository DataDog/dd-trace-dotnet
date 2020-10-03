using System;
using System.Configuration;
using System.Web.Http;

namespace Samples.AspNetMvc5
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            string enableSystemDiagnosticsTracing = ConfigurationManager.AppSettings["EnableSystemDiagnosticsTracing"] ??
                                                    Environment.GetEnvironmentVariable("EnableSystemDiagnosticsTracing");

            if (string.Equals(enableSystemDiagnosticsTracing, "true", StringComparison.OrdinalIgnoreCase))
            {
                config.EnableSystemDiagnosticsTracing();
            }

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                                       name: "DefaultApi",
                                       routeTemplate: "api/{controller}/{id}",
                                       defaults: new { id = RouteParameter.Optional }
                                      );
        }
    }
}
