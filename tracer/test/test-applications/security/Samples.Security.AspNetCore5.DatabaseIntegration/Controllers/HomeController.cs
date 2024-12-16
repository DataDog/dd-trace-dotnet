using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;

#pragma warning disable ASP0019 // warning ASP0019: Use IHeaderDictionary.Append or the indexer to append or set headers. IDictionary.Add will throw an ArgumentException when attempting to add a duplicate key
namespace Samples.Security.AspNetCore5.Controllers.DatabaseIntegration
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

            Response.Headers.Add("x-content-type-options", "nosniff");
            return View(envVars.ToList());
        }

        public IActionResult Privacy() => View();
    }
}
