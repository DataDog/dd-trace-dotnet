using System.Collections.Generic;
using Datadog.Trace;
using Microsoft.AspNetCore.Mvc;
using Samples.AspNetCoreMvc2.Extensions;
using Samples.Shared.Web;

namespace Samples.AspNetCoreMvc2.Controllers
{
    /*
    Core MVC
    http://localhost:54566/distributed/

    MVC
    http://localhost:50449/distributed/

    Web API
    http://localhost:50449/api/distributed/

    Core MVC (API) *this one*
    http://localhost:54566/api/distributed/last/
    */

    public class DistributedTracingApiController : ControllerBase
    {
        [Route("api/distributed/last")]
        public IActionResult Distributed()
        {
            var spanContext = Request.Headers.Extract();

            // this scope is a placeholder so we don't break the distributed
            // tracing chain because we don't support ASP.NET Core MVC yet
            using (var scope = Tracer.Instance.StartActive("manual", spanContext))
            {
                var model = new DistributedTracingModel();

                model.AddSpan(
                    $"{typeof(DistributedTracingApiController).FullName}.{nameof(Distributed)}",
                    scope?.Span.TraceId,
                    scope?.Span.SpanId);

                return Ok(model);
            }
        }
    }
}
