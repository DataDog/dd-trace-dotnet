using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Datadog.Trace;
using Samples.Shared.Web;

namespace Samples.AspNetMvc5.Controllers
{
    /*
    Core MVC
    http://localhost:54566/distributed/

    MVC
    http://localhost:50449/distributed/

    Web API *this one*
    http://localhost:50449/api/distributed/

    Core MVC (API) *next one*
    http://localhost:54566/api/distributed/last/
    */

    public class DistributedTracingApiController : ApiController
    {
        [HttpGet]
        [Route("api/distributed/")]
        public async Task<IHttpActionResult> Distributed()
        {
            var span = Tracer.Instance.ActiveScope?.Span;

            using (var client = new HttpClient())
            {
                var model = await client.GetAsync<DistributedTracingModel>("http://localhost:54566/api/distributed/last/");

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

                return Json(model);
            }
        }
    }
}
