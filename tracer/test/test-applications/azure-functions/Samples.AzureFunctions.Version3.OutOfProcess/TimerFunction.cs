using System;
using System.Net.Http;
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
            await CallFunctionHttp("simple");
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
