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

        public ActionResult Shutdown()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.DefinedTypes)
                {
                    if (type.Namespace == "Coverlet.Core.Instrumentation.Tracker")
                    {
                        var unloadModuleMethod = type.GetMethod("UnloadModule", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        unloadModuleMethod.Invoke(null, new object[] { this, EventArgs.Empty });
                    }
                }
            }

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
    }
}
