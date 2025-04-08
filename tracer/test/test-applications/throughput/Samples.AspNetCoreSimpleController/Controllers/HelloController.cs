using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;

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
            // Add 1000 spans to the current automatically instrumented trace
            var activeScope = Datadog.Trace.Tracer.Instance.ActiveScope;
            if (activeScope != null)
            {
                for (int i = 0; i < 1000; i++)
                {
                    using (var spanScope = Tracer.Instance.StartActive($"auto-span-{i}"))
                    {
                        spanScope.Span.SetTag("location", "auto");
                    }
                }
                // Force the span from integration to finish
                activeScope.Span.Finish()
            }
            // Create 1000 new traces, each with 1000 spans
            for (int traceIndex = 0; traceIndex < 1000; traceIndex++)
            {
                using (var traceScope = Tracer.Instance.StartActive($"manual-trace-{traceIndex}"))
                {
                    for (int spanIndex = 0; spanIndex < 1000; spanIndex++)
                    {
                        using (var spanScope = Tracer.Instance.StartActive($"manual-span-{traceIndex}-{spanIndex}"))
                        {
                            spanScope.Span.SetTag("location", "manual");
                        }
                    }
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
