﻿using System.Web.Mvc;

namespace Samples.AspNetMvc5.Areas.Datadog
{
    public class DatadogAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Datadog";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                "Datadog_default",
                "Datadog/{controller}/{action}/{id}",
                //new { area = AreaName, action = "Index", id = UrlParameter.Optional }
                new { area = AreaName, controller = "DogHouse", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
