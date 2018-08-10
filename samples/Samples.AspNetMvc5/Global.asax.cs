using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Samples.AspNetMvc5
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}
