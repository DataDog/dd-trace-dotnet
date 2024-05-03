using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Samples.Security.AspNetCore5.Models;
using System;
using System.Diagnostics;
using System.Linq;

namespace Samples.Security.AspNetCore5.Controllers
{
    public class UserController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public UserController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index(string userId = null)
        {
            Samples.SampleHelpers.SetUser(userId ?? "user3");

            return View();
        }
    }
}
