using System;
using System.Configuration;
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
      
            if (bool.TryParse(ConfigurationManager.AppSettings["CreateDb"], out bool res) && res)
            {
                DatabaseHelper.CreateAndFeedDatabase(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
            }
        }
        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (bool.TryParse(ConfigurationManager.AppSettings["dosomething-outside-mvc"], out bool res) && res)
            {
                HttpContext.Current.Response.Write($"do something before asp.net mvc cycle starts, with the request which query string is {HttpContext.Current.Request.QueryString}");
            }
        }



    }
}
