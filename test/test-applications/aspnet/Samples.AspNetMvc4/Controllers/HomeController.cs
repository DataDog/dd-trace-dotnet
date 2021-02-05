using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Samples.AspNetMvc4.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var prefixes = new[] { "COR_", "CORECLR_", "DD_", "DATADOG_" };

            var envVars = from envVar in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                          from prefix in prefixes
                          let key = (envVar.Key as string)?.ToUpperInvariant()
                          let value = envVar.Value as string
                          where key.StartsWith(prefix)
                          orderby key
                          select new KeyValuePair<string, string>(key, value);

            return View(envVars.ToList());
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult StatusCode(int value)
        {
            Response.StatusCode = value;
            ViewBag.Message = "Status code set to " + value;
            return View("About");
        }

        public ActionResult BadRequest()
        {
            throw new Exception("Oops, it broke.");
        }

        public ActionResult Identifier(int id)
        {
            ViewBag.Message = "Identifier set to " + id;
            return View("About");
        }
    }
}
