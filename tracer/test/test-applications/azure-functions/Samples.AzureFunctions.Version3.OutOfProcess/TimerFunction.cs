using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;

namespace Samples.AzureFunctions.Version3.OutOfProcess
{
    public class TimerFunction
    {
        private const string SecondsInterval = "*/45 * * * * *";
        private readonly HttpClient _httpClient = new HttpClient();

        //public TimerFunction(IHttpClientFactory httpClientFactory)
        //{
        //    _httpClient = httpClientFactory.CreateClient();
        //}

        [Function("DistributedHttpTimer")]
        public async Task TriggerAllTimer([TimerTrigger(SecondsInterval)] TimerInfo myTimer)
        {
            // log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            var datadogTraceDll = Assembly.Load("Datadog.Trace");
            var datadogTracer = datadogTraceDll.GetType("Datadog.Trace.Tracer");
            var method = datadogTracer.GetMethods().Where(m => m.Name == "StartActive").Single();
            var instanceProperty = datadogTracer.GetProperties().Where(p => p.Name == "Instance").Single();
            var tracerInstanceGetter = instanceProperty.GetGetMethod();
            var tracerInstance = tracerInstanceGetter.Invoke(null, new object[0]);
            var parameters = new object[] { "uber-reflection", null, null, null, false, true };
            using (var scope = (IDisposable)method.Invoke(tracerInstance, parameters)) {
                await CallFunctionHttp("simple");
            }
        }

        private async Task<string> CallFunctionHttp(string path)
        {
            var httpFunctionUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:7071";
            var url = $"http://{httpFunctionUrl}";
            var simpleResponse = await _httpClient.GetStringAsync($"{url}/api/{path}");
            return simpleResponse;
        }
    }
}
