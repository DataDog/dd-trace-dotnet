using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Samples.AspNetMvc4.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "AdminArea_default",
                "Admin/{controller}/{action}/{id}",
                new { area = "admin", controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
