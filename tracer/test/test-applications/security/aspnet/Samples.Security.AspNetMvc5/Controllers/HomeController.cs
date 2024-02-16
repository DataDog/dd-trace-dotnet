using System.Linq;
using System.Web;
using System.Web.Mvc;
using Samples.AspNetMvc5.Models;
using Samples.Security.AspNetMvc5.Models;
using ActionResult = System.Web.Mvc.ActionResult;

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

        public ActionResult LangHeader()
        {
            Response.AppendHeader("content-language", "krypton");

            return Content("Setting content-language");
        }

        [HttpPost]
        public ActionResult Upload(MiscModel miscModel)
        {
            ViewBag.Message = "Your upload page. Upload message: " + miscModel.Property1;
            return View(miscModel);
        }
        
        [HttpPost]
        [Route("/home/apisecurity/{id:int}")]
        public ActionResult ApiSecurity(int id, ApiSecurityModel model) => Json(new { Id = model.Dog, Message = $"{model.Dog2}-response", PathParamId = id });

        [HttpPost]
        [Route("api/home/emptymodel")]
        public ActionResult EmptyModel(ApiSecurityModel model)
        {
            return Json(null);
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
