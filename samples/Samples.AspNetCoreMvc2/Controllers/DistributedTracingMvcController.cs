using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.ExtensionMethods;
using Microsoft.AspNetCore.Mvc;
using Samples.Shared.Web;

namespace Samples.AspNetCoreMvc2.Controllers
{
    /*
    Core MVC *this one*
    http://localhost:54566/distributed/

    MVC *next one*
    http://localhost:50449/distributed/

    Web API
    http://localhost:50449/api/distributed/

    Core MVC (API)
    http://localhost:54566/api/distributed/last/
    */

    public class DistributedTracingMvcController : Controller
    {
        [Route("distributed")]
        public async Task<IActionResult> Distributed()
        {
            var span = Tracer.Instance.ActiveScope?.Span;
            span?.SetTraceSamplingPriority(SamplingPriority.UserKeep);

            using (var client = new HttpClient())
            {
                var model = await client.GetAsync<DistributedTracingModel>("http://localhost:50449/distributed");

                if (span != null)
                {
                    model.AddSpan(
                        $"{typeof(DistributedTracingMvcController).FullName}.{nameof(Distributed)}",
                        span.ServiceName,
                        span.OperationName,
                        span.ResourceName,
                        span.TraceId,
                        span.SpanId);
                }

                return Json(model);
            }
        }
    }
}
