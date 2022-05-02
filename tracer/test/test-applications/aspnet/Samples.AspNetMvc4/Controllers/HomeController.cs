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
            var envVars = SampleHelpers.GetDatadogEnvironmentVariables();

            return View(envVars.ToList());
        }

        public ActionResult Shutdown()
        {
            SampleHelpers.RunShutDownTasks(this);

            return RedirectToAction("Index");
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

        public ActionResult BadRequestWithStatusCode(int statuscode)
        {
            HttpContext.Items["ErrorStatusCode"] = statuscode;
            throw new Exception("Oops, it broke. Specified status code was: " + statuscode);
        }

        public ActionResult Identifier(int id)
        {
            ViewBag.Message = "Identifier set to " + id;
            return View("About");
        }

        public ActionResult OptionalIdentifier(int? id)
        {
            ViewBag.Message = "Identifier set to " + id;
            return View("About");
        }
    }
}
