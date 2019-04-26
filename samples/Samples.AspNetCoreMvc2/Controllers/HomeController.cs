using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Samples.AspNetCoreMvc2.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
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

        [Route("delay/{seconds}")]
        public IActionResult Delay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return View(seconds);
        }

        [Route("delay-async/{seconds}")]
        public async Task<IActionResult> DelayAsync(int seconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            return View("Delay", seconds);
        }

        [Route("bad-request")]
        public IActionResult ThrowException()
        {
            throw new Exception("This was a bad request.");
        }
    }
}
