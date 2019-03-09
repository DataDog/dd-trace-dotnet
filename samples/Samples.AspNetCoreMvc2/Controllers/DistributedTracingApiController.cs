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
            var propagatedContext = Request.Headers.Extract();

            // this scope is a placeholder so we don't break the distributed
            // tracing chain because we don't support ASP.NET Core MVC yet
            using (var scope = Tracer.Instance.StartActive("manual", propagatedContext))
            {
                // don't return too fast so it's more visible in the UI
                Thread.Sleep(TimeSpan.FromSeconds(1));

                var model = new DistributedTracingModel();

                model.AddSpan(
                    $"{typeof(DistributedTracingApiController).FullName}.{nameof(Distributed)}",
                    scope?.Span.ServiceName,
                    scope?.Span.OperationName,
                    scope?.Span.ResourceName,
                    scope?.Span.TraceId,
                    scope?.Span.SpanId);

                return Ok(model);
            }
        }
    }
}
