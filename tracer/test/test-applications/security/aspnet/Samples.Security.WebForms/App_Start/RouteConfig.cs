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
            var settings = new FriendlyUrlSettings();
            settings.AutoRedirectMode = RedirectMode.Permanent;
            routes.EnableFriendlyUrls(settings);
        }
    }
}
