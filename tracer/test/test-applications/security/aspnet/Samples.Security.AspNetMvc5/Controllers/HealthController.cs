using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Samples.AspNetMvc5.Controllers
{
    public class HealthController : Controller
    {
        [ValidateInput(false)]
        public ActionResult Index()
        {
            "testy-string".Normalize(NormalizationForm.FormKC);
            return View();
        }

        [ValidateInput(false)]
        public ActionResult Params(string id)
        {
            "testy-string".Normalize(NormalizationForm.FormKC);
            return Content($"Hello {id}\n");
        }

        public ActionResult About()
        {
            "testy-string".Normalize(NormalizationForm.FormKC);
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            "testy-string".Normalize(NormalizationForm.FormKC);
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}
