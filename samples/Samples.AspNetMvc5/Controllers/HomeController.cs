using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Samples.AspNetMvc5.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [Route("delay/{seconds}")]
        public ActionResult Delay(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            return View(seconds);
        }

        [Route("delay-async/{seconds}")]
        public async Task<ActionResult> DelayAsync(int seconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            return View("Delay", seconds);
        }
    }
}
