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
                name: "ApiConventions",
                routeTemplate: "api2/{action}/{value}",
                defaults: new
                {
                    controller = "Conventions", 
                    value = RouteParameter.Optional
                });
        }
    }
}
