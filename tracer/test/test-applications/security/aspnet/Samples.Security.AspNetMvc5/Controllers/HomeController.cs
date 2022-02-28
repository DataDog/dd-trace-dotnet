using System.Linq;
using System.Web.Mvc;
using Samples.AspNetMvc5.Models;

namespace Samples.AspNetMvc5.Controllers
{
    public class HomeController : Controller
    {
        [ValidateInput(false)]
        public ActionResult Index()
        {
            var envVars = SampleHelpers.GetDatadogEnvironmentVariables();

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

        [HttpPost]
        public ActionResult Upload(MiscModel miscModel)
        {
            ViewBag.Message = "Your upload page. Upload message: " + miscModel.Property1;

            return View();
        }

        public ActionResult Shutdown()
        {
            SampleHelpers.RunShutDownTasks(this);

            return RedirectToAction("Index");
        }
    }
}
