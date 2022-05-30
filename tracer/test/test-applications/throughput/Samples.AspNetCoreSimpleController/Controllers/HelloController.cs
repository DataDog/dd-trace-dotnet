using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

namespace Samples.AspNetCoreSimpleController.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HelloController : ControllerBase
    {
        private readonly ILogger<HelloController> _logger;

        public HelloController(ILogger<HelloController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public string Get() => "Hello world";

        [HttpGet]
        public string Exception()
        {
            try
            {
                throw new InvalidOperationException("Expected");
            }
            catch
            {
            }

            return "InvalidOperationException";
        }
    }
}
