using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Samples.AspNetMvc5.Data;

namespace Samples.AspNetMvc5
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
      
            if (bool.TryParse(ConfigurationManager.AppSettings["CreateDb"], out bool res) && res)
            {
                DatabaseHelper.CreateAndFeedDatabase(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
            }
        }
    }
}
