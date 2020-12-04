using System.Web.Http;
using Owin;

// [assembly: OwinStartup(typeof(Startup))]
namespace Samples.Owin.WebApi
{
    public class Startup
    {
        public static void Configuration(IAppBuilder appBuilder)
        {
            // the .NET Tracer should automatically inject the Trance/Span
            // appBuilder.Use<RequestLoggingOwinMiddleware>();

            var config = new HttpConfiguration();

            // Attribute routing.
            config.MapHttpAttributeRoutes();

            // Convention-based routing.
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            appBuilder.UseWebApi(config);
        }
    }
}
