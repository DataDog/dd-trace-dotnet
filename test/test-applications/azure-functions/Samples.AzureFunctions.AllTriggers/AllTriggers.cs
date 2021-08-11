using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Documents;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Samples.AzureFunctions.AllTriggers
{
	public static class AllTriggers
	{
		private static readonly HttpClient FunctionHttpClient = new HttpClient();
		private const string EveryTenSeconds = "*/10 * * * * *";

		[FunctionName("TriggerAllTimer")]
		public static async Task TriggerAllTimer([TimerTrigger(EveryTenSeconds)] TimerInfo myTimer, ILogger log)
		{
			log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
			await CallFunctionHttp("trigger");
		}

		[FunctionName("TimerTrigger")]
		public static void TimerTrigger([TimerTrigger(EveryTenSeconds)] TimerInfo myTimer, ILogger log)
		{
			log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
		}

		[FunctionName("SimpleHttpTrigger")]
		public static async Task<IActionResult> SimpleHttpTrigger(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "simple")] HttpRequest req,
			ILogger log)
		{
			log.LogInformation("C# HTTP trigger function processed a request.");

			string name = req.Query["name"];

			string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
			dynamic data = JsonConvert.DeserializeObject(requestBody);
			name = name ?? data?.name;

			string responseMessage = string.IsNullOrEmpty(name)
				? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
				: $"Hello, {name}. This HTTP triggered function executed successfully.";

			return new OkObjectResult(responseMessage);
		}

		[FunctionName("CosmosTrigger")]
		public static void Run([CosmosDBTrigger(
									databaseName: Config.CosmosDatabase,
									collectionName: Config.CosmosCollection,
									ConnectionStringSetting = Config.CosmosConnectionStringName,
									CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> documents,
							   ILogger log)
		{
			if (documents != null && documents.Count > 0)
			{
				log.LogInformation($"Documents modified: {documents.Count}");
				log.LogInformation($"First document Id: {documents[0].Id}");
			}
		}

		[FunctionName("BlobWatcherFunction")]
		public static void BlobTrigger([BlobTrigger("blob-function-sample/{name}", Connection = "StorageConnectionString")] Stream myBlob, string name, ILogger log)
		{
			log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
		}

		[FunctionName("TriggerCaller")]
		public static async Task<IActionResult> Trigger(
				[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "trigger")] HttpRequest req,
				ILogger log)
		{
			string triggersText = req.Query["types"];
			var triggers = triggersText?.ToLower()?.Split(",");
			var doAll = triggers == null || triggers.Length == 0 || triggers.Contains("all");

			if (doAll || triggers.Contains("blob"))
			{
				await Attempt(AddBlobForTrigger, log);
			}

			if (doAll || triggers.Contains("http"))
			{
				await Attempt(() => CallFunctionHttp("simple"), log);
			}

			if (doAll || triggers.Contains("cosmos"))
			{
				await Attempt(InsertIntoCosmos, log);
			}

			return new OkObjectResult("Attempting triggers.");
		}

		private static async Task<string> CallFunctionHttp(string path)
		{
			var httpFunctionUrl = Environment.GetEnvironmentVariable("DD_FUNCTION_HOST_BASE") ?? "http://localhost:7071";
			var url = $"{httpFunctionUrl}";
			var simpleResponse = await FunctionHttpClient.GetStringAsync($"{url}/api/{path}");
			return simpleResponse;
		}

		private static async Task AddBlobForTrigger()
		{
			var blobServiceClient = new BlobServiceClient(Config.StorageConnectionString);

			//Create a unique name for the container
			string containerName = "blob-function-sample";
			BlobContainerClient containerClient;

			var containers = blobServiceClient.GetBlobContainers();
			if (containers.All(c => c.Name != containerName))
			{
				containerClient = await blobServiceClient.CreateBlobContainerAsync(containerName);
			}
			else
			{
				containerClient = blobServiceClient.GetBlobContainerClient(containerName);
			}

			string fileName = "hello-" + Guid.NewGuid() + ".txt";

			await using MemoryStream stringInMemoryStream =
				new MemoryStream(ASCIIEncoding.Default.GetBytes("Your string here"));

			// Get a reference to a blob
			BlobClient blobClient = containerClient.GetBlobClient(fileName);

			// Upload data from the local file
			await blobClient.UploadAsync(stringInMemoryStream);
		}

		private static async Task InsertIntoCosmos()
		{
			var container = await Config.GetCosmosContainer();
			await container.CreateItemAsync<FakeEvent>(new FakeEvent() { id = Guid.NewGuid().ToString(), message = "See you on the other side." });
		}

		private static async Task Attempt(Func<Task> action, ILogger log)
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
