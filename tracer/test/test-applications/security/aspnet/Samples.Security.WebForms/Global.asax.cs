using System;
using System.Web;
using System.Web.Routing;

namespace Samples.Security.WebForms
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}
