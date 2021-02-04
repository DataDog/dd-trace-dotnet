using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Samples.AspNetMvc4.Areas.Admin.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var values = new Dictionary<string, string>
            {
                {"Area", GetRouteValueOrDefault("area") },
                {"Controller", GetRouteValueOrDefault("controller") },
                {"Action", GetRouteValueOrDefault("action") },
            };

            return View(values);
        }

        private string GetRouteValueOrDefault(string key)
        {
            return RouteData.Values.TryGetValue(key, out var value)
                       ? value.ToString()
                       : default;
        }
    }
}
