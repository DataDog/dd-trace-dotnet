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

        [HttpPost]
        public ActionResult UploadStruct(MiscModelStruct miscModel)
        {
            ViewBag.Message = "Your upload page. Upload message: " + miscModel.Property1;

            return View();
        }

        [HttpPost]
        public ActionResult UploadJson(MiscDictionaryModel miscModel)
        {
            if (miscModel != null)
            {
                ViewBag.Message = "Your upload page. Upload message: " + string.Join(", ", miscModel.DictionaryProperty?.Select(x => $"{x.Key}: {x.Value}") ?? Enumerable.Empty<string>());
            }
            else
            {
                ViewBag.Message = "Your upload page. Null model";
            }

            return View();
        }

        public ActionResult Shutdown()
        {
            SampleHelpers.RunShutDownTasks(this);

            return RedirectToAction("Index");
        }
    }
}
