using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

namespace Samples.AspNetCoreSimpleController.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EnvironmentController : ControllerBase
    {
        private readonly ILogger<EnvironmentController> _logger;

        public EnvironmentController(ILogger<EnvironmentController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public object Get() => Environment.GetEnvironmentVariables();
    }
}
