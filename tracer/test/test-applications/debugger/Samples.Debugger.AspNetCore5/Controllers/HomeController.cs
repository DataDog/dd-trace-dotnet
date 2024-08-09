using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Samples.Debugger.AspNetCore5.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {   
            ViewBag.ProfilerAttached = SampleHelpers.IsProfilerAttached();
            ViewBag.TracerAssemblyLocation = SampleHelpers.GetTracerAssemblyLocation();

            return Content("ok");
        }

        public IActionResult LangHeader()
        {
            Response.Headers.Add("content-language", "krypton");
            return Content("Setting content-language");
        }
        
        [AcceptVerbs("GET")]
        [Route("/null-action/{pathparam}/{pathparam2}")]
        public object NullAction(string pathparam, string pathparam2)
        {
            return null;
        }

        [HttpGet("/Recursive/{iterations}")]
        public async Task<int> Recursive([FromRoute] int iterations)
        {
            if (iterations <= 0)
            {
                throw new AbandonedMutexException($"The depth of iterations reached {iterations}");
            }

            await Task.Yield();
            return await PingPonged.Me(async (int iteration) => await Recursive(iteration), iterations - 1);
        }

        ref struct PingPonged
        {
            public static async Task<int> Me(Func<int, Task<int>> method, int iteration)
            {
                return await method(iteration);
            }
        }
    }
}
