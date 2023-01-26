using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Samples.Security.AspNetCore5.Models;
using System;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace;

namespace Samples.Security.AspNetCore5.Controllers
{
    public class UserController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public UserController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var userDetails = new UserDetails()
            {
                Id = "user3",
            };
            Tracer.Instance.ActiveScope?.Span.SetUser(userDetails);

            return View();
        }
    }
}
