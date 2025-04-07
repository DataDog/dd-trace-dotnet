using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using Datadog.Trace;

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
// #if MANUAL_INSTRUMENTATION
//             using var scope = Datadog.Trace.Tracer.Instance.StartActive("manual");
//             scope.Span.SetTag("location", "outer");
// #endif
#if MANUAL_INSTRUMENTATION
            for (var i = 0; i < 1000; i++)
            {
                var settings = new SpanCreationSettings()
                {
                    Parent = null
                };
                using var parent = Datadog.Trace.Tracer.Instance.StartActive($"root {i} span", settings);
                for (var j = 0; j < 1000; j++)
                {
                    using var child = Datadog.Trace.Tracer.Instance.StartActive($"child of {i}, {j} span", settings);
                }
            }
#endif
            return "Hello world";
        }

        [HttpGet]
        [Route("GetFiles")]
        public string GetFiles(string filter, string relativePath)
        {
            //Propagation
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            var finalPath = (currentPath + relativePath).Trim();
            StringBuilder sb = new StringBuilder();
            sb.Append(" ").Append(filter).Append(" ");
            filter = sb.ToString().Trim();

            //vulnerability
            var files = System.IO.Directory.GetFiles(finalPath, filter);

            //return the files
            return string.Join(",", files);
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
