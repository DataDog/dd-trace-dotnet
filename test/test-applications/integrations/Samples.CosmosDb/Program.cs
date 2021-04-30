using System;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;
using System.IO;
using System.Diagnostics;
using System.Net.Http;

namespace Samples.CosmosDb
{
    class Program
    {
        // The Azure Cosmos DB endpoint for running this sample.
        private static readonly string EndpointUri = ConfigurationManager.AppSettings["EndPointUri"];

        // The primary key for the Azure Cosmos account.
        private static readonly string PrimaryKey = ConfigurationManager.AppSettings["PrimaryKey"];

        // The Cosmos client instance
        private static CosmosEventListener cosmosEventListener;

        // The Cosmos client instance
        private CosmosClient cosmosClient;

        // The database we will create
        private Database database;

        // The container we will create.
        private Container container;

        // The name of the database and container we will create
        private string databaseId = "db";
        private string containerId = "items";

        // <Main>
        public static async Task Main(string[] args)
        {
            cosmosEventListener = new CosmosEventListener();

            try
            {
                Console.WriteLine($"{DateTime.Now:o}: Beginning operations...\n");
                Console.WriteLine($"{DateTime.Now:o}: Environment.Is64BitProcess: {Environment.Is64BitProcess}, args: {string.Join(",", args)}");

                Program p = new Program();
                await p.GetStartedDemoAsync();

            }
            catch (CosmosException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine($"{DateTime.Now:o}: {de.StatusCode} error occurred: {de}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"{DateTime.Now:o}: Error: {e}");
            }
        }
        // </Main>

        // <GetStartedDemoAsync>
        /// <summary>
        /// Entry point to call methods that operate on Azure Cosmos DB resources in this sample
        /// </summary>
        public async Task GetStartedDemoAsync()
        {
            // Create a new instance of the Cosmos Client
            var clientOptions = 
                new CosmosClientOptions() 
                { 
                    ApplicationName = "CosmosDBDotnetQuickstart", 
                    RequestTimeout = TimeSpan.FromMinutes(10),
                    OpenTcpConnectionTimeout = TimeSpan.FromMinutes(1),
                };

            cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, clientOptions);

            try
            {
                await CreateDatabaseAsync();
                await CreateContainerAsync();
                await ScaleContainerAsync();
                await AddItemsToContainerAsync();
                await QueryDatabasesAsync();
                await QueryContainersAsync();
                await QueryUsersAsync();
                await QueryItemsAsync();
            }
            finally
            {
                await DeleteDatabaseAndCleanupAsync();
            }
        }
        // </GetStartedDemoAsync>

        // <CreateDatabaseAsync>
        /// <summary>
        /// Create the database if it does not exist
        /// </summary>
        private async Task CreateDatabaseAsync()
        {
            // Create a new database
            database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            Console.WriteLine($"{DateTime.Now:o}: Created Database: {database.Id}\n");

            var user = await database.CreateUserAsync("user");
            Console.WriteLine($"{DateTime.Now:o}: Created user: {user.Resource.Id}\n");
        }
        // </CreateDatabaseAsync>

        // <CreateContainerAsync>
        /// <summary>
        /// Create the container if it does not exist. 
        /// Specifiy "/LastName" as the partition key since we're storing family information, to ensure good distribution of requests and storage.
        /// </summary>
        /// <returns></returns>
        private async Task CreateContainerAsync()
        {
            // Create a new container
            container = await database.CreateContainerIfNotExistsAsync(containerId, "/LastName", 400);
            Console.WriteLine($"{DateTime.Now:o}:Created Container: {container.Id}\n");
        }
        // </CreateContainerAsync>

        // <ScaleContainerAsync>
        /// <summary>
        /// Scale the throughput provisioned on an existing Container.
        /// You can scale the throughput (RU/s) of your container up and down to meet the needs of the workload. Learn more: https://aka.ms/cosmos-request-units
        /// </summary>
        /// <returns></returns>
        private async Task ScaleContainerAsync()
        {
            // Read the current throughput
            int? throughput = await container.ReadThroughputAsync();
            if (throughput.HasValue)
            {
                Console.WriteLine($"{DateTime.Now:o}:Current provisioned throughput : {throughput.Value}\n");
                int newThroughput = throughput.Value + 100;
                // Update throughput
                await container.ReplaceThroughputAsync(newThroughput);
                Console.WriteLine($"{DateTime.Now:o}:New provisioned throughput : {newThroughput}\n");
            }
            
        }
        // </ScaleContainerAsync>

        // <AddItemsToContainerAsync>
        /// <summary>
        /// Add Family items to the container
        /// </summary>
        private async Task AddItemsToContainerAsync()
        {
            // Create a family object for the Andersen family
            Family andersenFamily = new Family
            {
                Id = "Andersen.1",
                LastName = "Andersen",
                Parents = new Parent[]
                {
                    new Parent { FirstName = "Thomas" },
                    new Parent { FirstName = "Mary Kay" }
                },
                Children = new Child[]
                {
                    new Child
                    {
                        FirstName = "Henriette Thaulow",
                        Gender = "female",
                        Grade = 5,
                        Pets = new Pet[]
                        {
                            new Pet { GivenName = "Fluffy" }
                        }
                    }
                },
                Address = new Address { State = "WA", County = "King", City = "Seattle" },
                IsRegistered = false
            };

            try
            {
                // Read the item to see if it exists.  
                ItemResponse<Family> andersenFamilyResponse = await container.ReadItemAsync<Family>(andersenFamily.Id, new PartitionKey(andersenFamily.LastName));
                Console.WriteLine($"{DateTime.Now:o}:Item in database with id: {andersenFamilyResponse.Resource.Id} already exists\n");
            }
            catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                ItemResponse<Family> andersenFamilyResponse = await container.CreateItemAsync<Family>(andersenFamily, new PartitionKey(andersenFamily.LastName));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine($"{DateTime.Now:o}:Created item in database with id: {andersenFamilyResponse.Resource.Id} Operation consumed {andersenFamilyResponse.RequestCharge} RUs.\n");
            }

            // Create a family object for the Wakefield family
            Family wakefieldFamily = new Family
            {
                Id = "Wakefield.7",
                LastName = "Wakefield",
                Parents = new Parent[]
                {
                    new Parent { FamilyName = "Wakefield", FirstName = "Robin" },
                    new Parent { FamilyName = "Miller", FirstName = "Ben" }
                },
                Children = new Child[]
                {
                    new Child
                    {
                        FamilyName = "Merriam",
                        FirstName = "Jesse",
                        Gender = "female",
                        Grade = 8,
                        Pets = new Pet[]
                        {
                            new Pet { GivenName = "Goofy" },
                            new Pet { GivenName = "Shadow" }
                        }
                    },
                    new Child
                    {
                        FamilyName = "Miller",
                        FirstName = "Lisa",
                        Gender = "female",
                        Grade = 1
                    }
                },
                Address = new Address { State = "NY", County = "Manhattan", City = "NY" },
                IsRegistered = true
            };

            try
            {
                // Read the item to see if it exists
                ItemResponse<Family> wakefieldFamilyResponse = await container.ReadItemAsync<Family>(wakefieldFamily.Id, new PartitionKey(wakefieldFamily.LastName));
                Console.WriteLine($"{DateTime.Now:o}:Item in database with id: {wakefieldFamilyResponse.Resource.Id} already exists\n");
            }
            catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container representing the Wakefield family. Note we provide the value of the partition key for this item, which is "Wakefield"
                ItemResponse<Family> wakefieldFamilyResponse = await container.CreateItemAsync<Family>(wakefieldFamily, new PartitionKey(wakefieldFamily.LastName));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine($"{DateTime.Now:o}:Created item in database with id: {wakefieldFamilyResponse.Resource.Id} Operation consumed {wakefieldFamilyResponse.RequestCharge} RUs.\n");
            }
        }
        // </AddItemsToContainerAsync>

        // <QueryItemsAsync>
        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// </summary>
        private async Task QueryDatabasesAsync()
        {
            var sqlQueryText = "SELECT * FROM d";
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

            Console.WriteLine("Running queries: {0}\n", sqlQueryText);

            Console.WriteLine($"{DateTime.Now:o}: cosmosClient.GetDatabaseQueryStreamIterator-QueryDefinition");
            await ExecAndIterateQueryAsync(() => cosmosClient.GetDatabaseQueryStreamIterator(queryDefinition));
            Console.WriteLine($"{DateTime.Now:o}: cosmosClient.GetDatabaseQueryStreamIterator-string");
            await ExecAndIterateQueryAsync(() => cosmosClient.GetDatabaseQueryStreamIterator(sqlQueryText));
            Console.WriteLine($"{DateTime.Now:o}: cosmosClient.GetDatabaseQueryIterator-QueryDefinition");
            await ExecAndIterateQueryAsync(() => cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>(queryDefinition));
            Console.WriteLine($"{DateTime.Now:o}: cosmosClient.GetDatabaseQueryIterator-string");
            await ExecAndIterateQueryAsync(() => cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>(sqlQueryText));

        }

        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// </summary>
        private async Task QueryContainersAsync()
        {
            var sqlQueryText = "SELECT * FROM c";
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

            Console.WriteLine("Running queries: {0}\n", sqlQueryText);

            Console.WriteLine($"{DateTime.Now:o}: database.GetContainerQueryStreamIterator-QueryDefinition");
            await ExecAndIterateQueryAsync(() => database.GetContainerQueryStreamIterator(queryDefinition));
            Console.WriteLine($"{DateTime.Now:o}: database.GetContainerQueryStreamIterator-string");
            await ExecAndIterateQueryAsync(() => database.GetContainerQueryStreamIterator(sqlQueryText));
            Console.WriteLine($"{DateTime.Now:o}: database.GetContainerQueryIterator-QueryDefinition");
            await ExecAndIterateQueryAsync(() => database.GetContainerQueryIterator<Family>(queryDefinition));
            Console.WriteLine($"{DateTime.Now:o}: database.GetContainerQueryIterator-string");
            await ExecAndIterateQueryAsync(() => database.GetContainerQueryIterator<Family>(sqlQueryText));
        }

        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// </summary>
        private async Task QueryUsersAsync()
        {
            var sqlQueryText = "SELECT * FROM u";
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

            Console.WriteLine("Running queries: {0}\n", sqlQueryText);

            Console.WriteLine($"{DateTime.Now:o}: database.GetUserQueryIterator-QueryDefinition");
            await ExecAndIterateQueryAsync(() => database.GetUserQueryIterator<UserProperties>(queryDefinition));
            Console.WriteLine($"{DateTime.Now:o}: database.GetUserQueryIterator-string");
            await ExecAndIterateQueryAsync(() => database.GetUserQueryIterator<UserProperties>(sqlQueryText));
        }

        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// </summary>
        private async Task QueryItemsAsync()
        {
            var sqlQueryText = "SELECT * FROM c WHERE c.LastName = 'Andersen'";
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

            Console.WriteLine("Running queries: {0}\n", sqlQueryText);

            Console.WriteLine($"{DateTime.Now:o}: container.GetItemQueryStreamIterator-QueryDefinition");
            await ExecAndIterateQueryAsync(() => container.GetItemQueryStreamIterator(queryDefinition));
            Console.WriteLine($"{DateTime.Now:o}: container.GetItemQueryStreamIterator-string");
            await ExecAndIterateQueryAsync(() => container.GetItemQueryStreamIterator(sqlQueryText));
            Console.WriteLine($"{DateTime.Now:o}: container.GetItemQueryIterator-QueryDefinition");
            await ExecAndIterateQueryAsync(() => container.GetItemQueryIterator<Family>(queryDefinition));
            Console.WriteLine($"{DateTime.Now:o}: container.GetItemQueryIterator-string");
            await ExecAndIterateQueryAsync(() => container.GetItemQueryIterator<Family>(sqlQueryText));
        }

        private async Task ExecAndIterateQueryAsync(Func<FeedIterator> query)
        {

            FeedIterator queryResultSetIterator = query();

            while (queryResultSetIterator.HasMoreResults)
            {
                var currentResultSet = await queryResultSetIterator.ReadNextAsync();
                var sr = new StreamReader(currentResultSet.Content);
                Console.WriteLine("\tRead {0}\n", sr.ReadToEnd());
            }
        }

        private async Task ExecAndIterateQueryAsync<T>(Func<FeedIterator<T>> query)
        {

            FeedIterator<T> queryResultSetIterator = query();

            while (queryResultSetIterator.HasMoreResults)
            {
                var currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (var item in currentResultSet)
                {
                    Console.WriteLine("\tRead {0}\n", item);
                }
            }
        }
        // </QueryItemsAsync>

        // <DeleteDatabaseAndCleanupAsync>
        /// <summary>
        /// Delete the database and dispose of the Cosmos Client instance
        /// </summary>
        private async Task DeleteDatabaseAndCleanupAsync()
        {
            var deleteTask = database?.DeleteAsync();
            if (deleteTask != null)
            {
                DatabaseResponse databaseResourceResponse = await deleteTask;
                // Also valid: await cosmosClient.Databases["FamilyDatabase"].DeleteAsync();
            }

            Console.WriteLine("Deleted Database: {0}\n", databaseId);

            //Dispose of CosmosClient
            cosmosClient.Dispose();
        }
        // </DeleteDatabaseAndCleanupAsync>
    }
}
