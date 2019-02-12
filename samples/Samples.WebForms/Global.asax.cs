using System;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;

namespace Samples.WebForms
{
    public class Global : HttpApplication
    {
        private void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            RegisterCustomRoutes(RouteTable.Routes);
        }

        private void RegisterCustomRoutes(RouteCollection routes)
        {
            routes.MapPageRoute(
                                "ProductsByCategoryRoute",
                                "Category/{categoryName}",
                                "~/ProductList.aspx");

            routes.MapPageRoute(
                                "ProductByNameRoute",
                                "Product/{productName}",
                                "~/ProductDetails.aspx");
        }
    }
}
