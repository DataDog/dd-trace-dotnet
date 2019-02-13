using System;
using System.Web;
using System.Web.Routing;

namespace Samples.WebForms
{
    public class Global : HttpApplication
    {
        private void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}
