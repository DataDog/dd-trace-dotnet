using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Samples.AzureFunctions.AllTriggers
{
    public class AllTriggers
	{
		private const string EveryTenSeconds = "*/10 * * * * *";

		private readonly HttpClient _httpClient;

		public AllTriggers(IHttpClientFactory httpClientFactory)
		{
			_httpClient = httpClientFactory.CreateClient();
		}

		[FunctionName("TriggerAllTimer")]
		public async Task TriggerAllTimer([TimerTrigger(EveryTenSeconds)] TimerInfo myTimer, ILogger log)
		{
			log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
			await CallFunctionHttp("trigger");
		}

		[FunctionName("TimerTrigger")]
		public void TimerTrigger([TimerTrigger(EveryTenSeconds)] TimerInfo myTimer, ILogger log)
		{
			log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
		}

		[FunctionName("SimpleHttpTrigger")]
		public async Task<IActionResult> SimpleHttpTrigger(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "simple")] HttpRequest req,
			ILogger log)
		{
			log.LogInformation("C# HTTP trigger function processed a request.");
			return new OkObjectResult("This HTTP triggered function executed successfully. ");
		}

		[FunctionName("TriggerCaller")]
		public async Task<IActionResult> Trigger(
				[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "trigger")] HttpRequest req,
				ILogger log)
		{
			string triggersText = req.Query["types"];
			var triggers = triggersText?.ToLower()?.Split(",");
			var doAll = triggers == null || triggers.Length == 0 || triggers.Contains("all");

			if (doAll || triggers.Contains("http"))
			{
				await Attempt(() => CallFunctionHttp("simple"), log);
			}

			return new OkObjectResult("Attempting triggers.");
		}

		private async Task<string> CallFunctionHttp(string path)
		{
			var httpFunctionUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:7071";
			var url = $"http://{httpFunctionUrl}";
			var simpleResponse = await _httpClient.GetStringAsync($"{url}/api/{path}");
			return simpleResponse;
		}

		private async Task Attempt(Func<Task> action, ILogger log)
		{
			try
			{
				await action();
			}
			catch (Exception ex)
			{
				log.LogError(ex, "Trigger attempt failure");
			}
		}
	}
}
