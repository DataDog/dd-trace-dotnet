using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Samples.AspNetMvc5.Controllers
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

        public ActionResult Get(int id)
        {
            return View("Delay", id);
        }

        [Route("delay/{seconds}")]
        public ActionResult Delay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return View(seconds);
        }
        
        [Route("delay-optional/{seconds?}")]
        public ActionResult Optional(int? seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds ?? 0));
            return View("Delay", seconds ?? 0);
        }

        [Route("delay-async/{seconds}")]
        public async Task<ActionResult> DelayAsync(int seconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            return View("Delay", seconds);
        }

        [Route("statuscode/{value}")]
        public ActionResult StatusCode(int value)
        {
            Response.StatusCode = value;
            return Content("Status code set to " + value);
        }

        [Route("badrequest")]
        public ActionResult BadRequest()
        {
            throw new Exception("Oops, it broke.");
        }

        [Route("BadRequestWithStatusCode/{statuscode}")]
        public ActionResult BadRequestWithStatusCode(int statuscode)
        {
            HttpContext.Items["ErrorStatusCode"] = statuscode;
            throw new Exception("Oops, it broke. Specified status code was: " + statuscode);
        }
    }
}
