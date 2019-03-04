using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;
using Microsoft.AspNetCore.Mvc;
using Samples.AspNetCoreMvc2.Extensions;
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
            var spanContext = Request.Headers.Extract();

            // this scope is a placeholder so we don't break the distributed
            // tracing chain because we don't support ASP.NET Core MVC yet
            using (var scope = Tracer.Instance.StartActive("manual", spanContext))
            using (var client = new HttpClient())
            {
                var model = await client.GetAsync<DistributedTracingModel>("http://localhost:50449/distributed");

                model.AddSpan(
                    $"{typeof(DistributedTracingMvcController).FullName}.{nameof(Distributed)}",
                    scope?.Span.TraceId,
                    scope?.Span.SpanId);

                return Json(model);
            }
        }
    }
}
