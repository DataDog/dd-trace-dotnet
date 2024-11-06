using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Mvc;

namespace Samples.AspNet.MultipleAppsInDomain.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var results = new Results();
            results.Pid = Process.GetCurrentProcess().Id;
            results.AppConfig = new Dictionary<string, string>();
            foreach(var key in System.Configuration.ConfigurationManager.AppSettings.AllKeys)
            {
                results.AppConfig[key] = System.Configuration.ConfigurationManager.AppSettings[key];
            }

            return Json(results, JsonRequestBehavior.AllowGet);
        }

        public class Results
        {
            public int Pid { get; set; }
            public Dictionary<string, string> AppConfig { get; set; }
        }
    }
}