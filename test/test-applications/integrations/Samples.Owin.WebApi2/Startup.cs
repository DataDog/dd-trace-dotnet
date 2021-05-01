using System.Web.Http;
using Owin;

namespace Samples.Owin.WebApi2
{
    public class Startup
    {
        public static void Configuration(IAppBuilder appBuilder)
        {
            // Insert .NET Tracer before any other middleware so the Datadog trace
            // will wrap the rest of the OWIN pipeline
            // TODO: appBuilder.UseDatadogTracingOwinMiddleware();
            // appBuilder.Use<SomeOtherMiddleware>();

            var config = new HttpConfiguration();

            // Attribute routing.
            config.MapHttpAttributeRoutes();

            // Convention-based routing.
            config.Routes.MapHttpRoute( 
                name: "ApiConventions", 
                routeTemplate: "api2/{action}/{value}",
                defaults: new
                {
                    controller = "Conventions",
                    value = RouteParameter.Optional
                }); 


            appBuilder.UseWebApi(config);
        }
    }
}
