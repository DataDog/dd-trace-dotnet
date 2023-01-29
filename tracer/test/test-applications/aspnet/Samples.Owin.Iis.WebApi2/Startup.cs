using System.Web;
using System.IO;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using Microsoft.Owin;
using Owin;
using Microsoft.Owin.Extensions;
using Samples.AspNetMvc5.Handlers;
using Samples.Owin.WebApi2;
using System;

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
            ThrowSillyException();
            appBuilder.UseWebApi(config);
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
    }
}
