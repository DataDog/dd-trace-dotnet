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
        public string Get()
        {
#if MANUAL_INSTRUMENTATION
            using var scope = Datadog.Trace.Tracer.Instance.StartActive("manual");
            scope.Span.SetTag("location", "outer");
#endif
            return "Hello world";
        }

        [HttpGet]
        [Route("exception")]
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
