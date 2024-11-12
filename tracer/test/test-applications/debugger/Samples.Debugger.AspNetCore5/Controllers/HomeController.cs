using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using Samples.Probes.TestRuns;

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

        [HttpGet("/RunTest/{testName}")]
        public async Task RunTest([FromRoute] string testName)
        {
            var instance = GetInstance(testName);
            await RunTest(instance, testName);
        }
        private static async Task RunTest(object instance, string testClassName)
        {
            switch (instance)
            {
                case IRun run:
                    run.Run();
                    break;
                case IAsyncRun asyncRun:
                    await asyncRun.RunAsync();
                    break;
                default:
                    throw new Exception($"Test class not found: {testClassName}");
            }
        }

        private static object GetInstance(string testName)
        {
            var type = Assembly.GetAssembly(typeof(IRun)).GetType(testName);
            if (type == null)
            {
                throw new ArgumentException($"Type {testName} not found in assembly {Assembly.GetExecutingAssembly().GetName().Name}");
            }

            var instance = Activator.CreateInstance(type);
            return instance;
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
