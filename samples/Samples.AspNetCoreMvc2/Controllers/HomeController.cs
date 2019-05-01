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

        [Route("wee")]
        public async Task<IActionResult> Test()
        {
            Exception caught;

            try
            {
                await WeeAsync();
            }
            catch (AggregateException ex)
            {
                caught = ex;
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            try
            {
                await WeeSync();
            }
            catch (AggregateException ex)
            {
                caught = ex;
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            return View("Delay", 0);
        }

        private async Task<bool> WeeAsync()
        {
            await Task.Delay(1);
            await Task.FromResult(true);
            return await WeeNested(true);
        }

        private Task<bool> WeeSync()
        {
            var task = WeeNested(true);

            if (task.Result == true)
            {
                throw new Exception("wee sync");
            }

            return task;
        }

        private async Task<bool> WeeNested(bool val)
        {
            await Task.Delay(1);
            await Task.Run(
                () =>
                {
                    throw new Exception("WEEEEEEE!!!!");
                });
            return await Task.FromResult(val);
        }
    }
}
