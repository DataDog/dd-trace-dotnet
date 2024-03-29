using System.Web;
using System.IO;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using Microsoft.Owin;
using Owin;
using Microsoft.Owin.Extensions;
using Samples.AspNetMvc5.Handlers;
using Samples.Owin.WebApi2;

[assembly: OwinStartup(typeof(Startup))]

namespace Samples.Owin.WebApi2
{
    public class Startup
    {
        public static void Configuration(IAppBuilder appBuilder)
        {
            var config = new HttpConfiguration();

            // Attribute routing.
            config.MapHttpAttributeRoutes();

            // Replace default exception handler
            config.Services.Replace(typeof(IExceptionHandler), new CustomTracingExceptionHandler());

            // Add global message handler
            config.MessageHandlers.Add(new PassThroughQuerySuccessMessageHandler());

            // Convention-based routing.
            config.Routes.MapHttpRoute( 
                name: "ApiConventions", 
                routeTemplate: "api2/{action}/{value}",
                defaults: new
                {
                    controller = "Conventions",
                    value = RouteParameter.Optional
                });

            // Add a new /handler-api base path that will be handled by a per-route message handler
            config.Routes.MapHttpRoute(
                name: "Route2",
                routeTemplate: "handler-api/{controller}/{id}",
                defaults: new
                {
                    id = RouteParameter.Optional
                },
                constraints: null,
                handler: new TerminatingQuerySuccessMessageHandler()  // per-route message handler
            );

            appBuilder.UseWebApi(config);
        }
    }
}
