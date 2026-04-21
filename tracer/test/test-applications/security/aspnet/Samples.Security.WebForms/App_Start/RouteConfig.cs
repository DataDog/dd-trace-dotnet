using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;
using Microsoft.AspNet.FriendlyUrls;

namespace Samples.Security.WebForms
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.RouteExistingFiles = true;
            routes.MapPageRoute("HealthParams", "Health/Params/{id}/", "~/Health.aspx");
            routes.Add("RouteHandlerParams", new Route("RouteHandler/Params/{id}", new RouteHandlerParamsRouteHandler()));
            routes.Add("ApiSecurity", new Route("api/security/{id}", new ApiSecurityRouteHandler()));
            routes.MapPageRoute("Shutdown", "home/shutdown", "~/Default.aspx");
            var settings = new FriendlyUrlSettings();
            settings.AutoRedirectMode = RedirectMode.Permanent;
            routes.EnableFriendlyUrls(settings);
        }

        private sealed class RouteHandlerParamsRouteHandler : IRouteHandler
        {
            public IHttpHandler GetHttpHandler(RequestContext requestContext) => new RouteHandlerParamsHttpHandler();
        }

        private sealed class RouteHandlerParamsHttpHandler : IHttpHandler
        {
            public bool IsReusable => true;

            public void ProcessRequest(HttpContext context)
            {
                var id = context.Request.RequestContext.RouteData.Values["id"];

                context.Response.StatusCode = 200;
                context.Response.Write("id = " + id);
            }
        }
    }
}
