using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Http;

namespace Samples.AspNetMvc5
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}
