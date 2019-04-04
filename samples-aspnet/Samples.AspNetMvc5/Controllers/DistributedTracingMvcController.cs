using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using Datadog.Trace;
using Samples.Shared.Web;

namespace Samples.AspNetMvc5.Controllers
{
    /*
    Core MVC
    http://localhost:54566/distributed/

    MVC *this one*
    http://localhost:50449/distributed/

    Web API *next one*
    http://localhost:50449/api/distributed/

    Core MVC (API)
    http://localhost:54566/api/distributed/last/
    */

    public class DistributedTracingMvcController : Controller
    {
        [Route("distributed")]
        public async Task<ActionResult> Distributed()
        {
            var span = Tracer.Instance.ActiveScope?.Span;

            using (var client = new HttpClient())
            {
                var model = await client.GetAsync<DistributedTracingModel>("http://localhost:50449/api/distributed");

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

                return Json(model, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
