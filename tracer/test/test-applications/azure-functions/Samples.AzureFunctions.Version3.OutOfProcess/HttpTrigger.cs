using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Samples.AzureFunctions.Version3.OutOfProcess
{
    public class HttpTrigger
    {
        public HttpTrigger()
        {
        }

        [Function("Simple")]
        public async Task<HttpResponseData> Simple(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "simple")] HttpRequestData req,
            FunctionContext context)
        {
            await Task.Delay(5);
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
