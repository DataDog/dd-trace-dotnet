using System.Web.Routing;
using Microsoft.AspNet.FriendlyUrls;

namespace Samples.WebForms.Ninject
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.EnableFriendlyUrls();
        }
    }
}
