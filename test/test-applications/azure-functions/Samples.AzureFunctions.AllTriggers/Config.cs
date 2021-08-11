using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Samples.AzureFunctions.AllTriggers
{
	public static class Config
	{
		public const string RabbitMqQueueName = "devrabbitmqtrigger";
		public const string CosmosDatabase = "CosmosIOTDemo";
		public const string CosmosCollection = "CosmosIOTDemo";

		public const string StorageConnectionStringName = "StorageConnectionString";
		public const string CosmosConnectionStringName = "CosmosConnectionString";

		public static string StorageConnectionString => Environment.GetEnvironmentVariable(StorageConnectionStringName);
		public static string CosmosConnectionString => Environment.GetEnvironmentVariable(CosmosConnectionStringName);

		public static async Task<Container> GetCosmosContainer()
		{
			var cosmosClient = new CosmosClient(Config.CosmosConnectionString);
			var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(Config.CosmosDatabase);
			var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(Config.CosmosCollection, "/vin");
			var container = containerResponse.Container;
			return container;
		}
	}
}
