using System.Linq;
using System.Net.Http;
using System.Web.Mvc;
using Datadog.Trace;

namespace Samples.AspNet.VersionConflict.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var envVars = SampleHelpers.GetDatadogEnvironmentVariables();

            return View(envVars.ToList());
        }

        public ActionResult Shutdown()
        {
            SampleHelpers.RunShutDownTasks(this);

            return RedirectToAction("Index");
        }

        public ActionResult SendRequest()
        {
            int result = 0;

            using (Tracer.Instance.StartActive("Manual"))
            {
                using (var client = new HttpClient())
                {
                    var target = Url.Action("Index", "Home", null, "http");
                    var content = client.GetStringAsync(target).Result;
                    result = content.Length;
                }
            }

            return View(result);
        }        
    }
}
