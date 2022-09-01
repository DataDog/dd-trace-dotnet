using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Samples.Security.AspNetCore5.Models;
using System;
using System.Diagnostics;
using System.Linq;

namespace Samples.Security.AspNetCore5.Controllers
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

            var envVars = SampleHelpers.GetDatadogEnvironmentVariables();

            return View(envVars.ToList());
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
