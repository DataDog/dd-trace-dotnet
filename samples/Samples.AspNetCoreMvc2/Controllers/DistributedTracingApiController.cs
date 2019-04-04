using System;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.ExtensionMethods;
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
            // don't return too fast so it's more visible in the UI
            Thread.Sleep(100);

            var model = new DistributedTracingModel();

            var span = Tracer.Instance.ActiveScope?.Span;

            if (span != null)
            {
                model.AddSpan(
                    $"{typeof(DistributedTracingApiController).FullName}.{nameof(Distributed)}",
                    span.ServiceName,
                    span.OperationName,
                    span.ResourceName,
                    span.TraceId,
                    span.SpanId);
            }

            return Ok(model);
        }
    }
}
