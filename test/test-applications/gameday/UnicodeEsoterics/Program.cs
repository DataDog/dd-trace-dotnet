using System;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using Datadog.Trace;

namespace Samples.CosmosDb
{
    class Program
    {
        // The Azure Cosmos DB endpoint for running this sample.
        private static readonly string EndpointUri = ConfigurationManager.AppSettings["EndPointUri"];

        // The primary key for the Azure Cosmos account.
        private static readonly string PrimaryKey = ConfigurationManager.AppSettings["PrimaryKey"];

        // The Cosmos client instance
        private CosmosClient cosmosClient;

        // The database we will create
        private Database database;

        // The container we will create.
        private Container container;

        // The name of the database and container we will create
        private string databaseId = "db";
        private string containerId = "items";

        private readonly string[] queries = 
            new[] 
            {
                "SELECT * FROM x WHERE x.LastName = 'Andersen'",
                "SELECT * FROM α WHERE α.LastName = 'Andersen'",
                "SELECT * FROM a̵̡̡̢̡̡͙͈͚̖͎͕̣̲̪̮̬͖̯̠̫͈̫̹̟͈͚̳͈̽̍̈̄̈́͂͆͑̅̑̃͑̅̿̋̉̅̅̂̌͒̋̂͂̊̕͘͘͝ͅ WHERE a̵̡̡̢̡̡͙͈͚̖͎͕̣̲̪̮̬͖̯̠̫͈̫̹̟͈͚̳͈̽̍̈̄̈́͂͆͑̅̑̃͑̅̿̋̉̅̅̂̌͒̋̂͂̊̕͘͘͝ͅ.l̷̢̼̤͙͇͓̬̗͔̗͎̩͇͖̲̥̜̟̂̈́̓̑ͅa̵̡̡̢̡̡͙͈͚̖͎͕̣̲̪̮̬͖̯̠̫͈̫̹̟͈͚̳͈̽̍̈̄̈́͂͆͑̅̑̃͑̅̿̋̉̅̅̂̌͒̋̂͂̊̕͘͘͝ͅs̵̨̧̨̖͕̯͉̜̪̞̖̘̣̫͙͓̰͉͔͒̊́̎̌͂̂̆͂͜͝ẗ̸̗̗̙̫̙̣͓̩̞̩͇͎̙̹̼̠̝́͑̔́̽̾̈́́̊͂̒̈́̽͂̃̓͒̉̍̂͗͠͠͝͝ͅn̷͈̻͉̬̺̖̺̳̝̼̫͖͎̽̋̂͒̓̇̎͒͋͂̒̈́̚͘͠à̸̢̛̬͇̪͚̝̣͇̦̣̲̝͠m̸̢̛̗̞̥̮̱̥͔̋̂̐̑̆͗̓͊̿̊̍͋̄̊́̈́̆̋̾͗̀̊͘̚̚͘͠ę̸̜̻̮̝̘̝̳̭̖̩̘̮̺̟̌̔̉̈́̍͆͋̓͝ = 'A̸̺͔͚̣̘̘̗͊̏̾̈̅̅̌̓̌͒͝ǹ̸͔̜̪̪̣̰͕͎̝͕̲͙̔̓̈͗͘͜ḋ̸̜͚̙̼̯̯͉͙̙̹̹̼̜̦͚̞̈́̓͜ͅe̴̳͎͇̼̜̳̝̫͕̗͍͕̗͖͕̹͕̟̪͔̥̟͇̟͐̆̂͛̆́́̀͠ŗ̵̢̧͖̘̱͓̩͕̫̥͚͇̒̌̂͊̐̍̊̒̓̃̚͘͝s̷̢̛̭̳̪̹̦̲̳̙̓̈̓̒͊̄̆͗͝è̸̟͉͍̖̼̜̙͚̙̣̬͙̯͕̟̈́̄̉̊̐̋͐̆͌̈́̋͒̚͝͝ͅn̶̛̯͚̙͕̖̠͉̤̩̫̩'",
            };

        // <Main>
        public static async Task Main(string[] args)
        {
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
                await HandCraftedQueries();
                await MultiLingualQueries();
                StrangeScopes();
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

        private void StrangeScopes()
        {
            var tests = 
                new[] 
                {
                    ("script", "<script>alert(0);</script>"),
                    ("quotes", @"''""''""''""""'""'"),
                    ("comments", @"<!-- -->"),
                    ("bizar-text", "A̸̺͔͚̣̘̘̗͊̏̾̈̅̅̌̓̌͒͝ǹ̸͔̜̪̪̣̰͕͎̝͕̲͙̔̓̈͗͘͜ḋ̸̜͚̙̼̯̯͉͙̙̹̹̼̜̦͚̞̈́̓͜ͅe̴̳͎͇̼̜̳̝̫͕̗͍͕̗͖͕̹͕̟̪͔̥̟͇̟͐̆̂͛̆́́̀͠ŗ̵̢̧͖̘̱͓̩͕̫̥͚͇̒̌̂͊̐̍̊̒̓̃̚͘͝s̷̢̛̭̳̪̹̦̲̳̙̓̈̓̒͊̄̆͗͝è̸̟͉͍̖̼̜̙͚̙̣̬͙̯͕̟̈́̄̉̊̐̋͐̆͌̈́̋͒̚͝͝ͅn̶̛̯͚̙͕̖̠͉̤̩̫̩'"),
                    ("null", " \0abcd"),
                    ("bell", " \u0007abcd"),
                    ("SOH", " \u0001abcd"),
                    ("illegal-1", " X\uD800Y"),
                    ("illegal-2", " X0\uDBFFY"),
                };

            foreach ((var name, var test) in tests)
            {
                using var parentScope = Tracer.Instance.StartActive("manual.strangescopes");
                parentScope.Span.SetTag("manual.strangescopes.name", name);
                parentScope.Span.SetTag("manual.strangescopes.test", test);
            }
        }

        private async Task HandCraftedQueries()
        {
            foreach (var query in queries)
            {
                await QueryItemsAsync(query);
            }
        }
        private async Task MultiLingualQueries()
        {
            var lines = File.ReadAllLines("multilanguages.csv");
            foreach (var line in lines)
            {
                using (var parentScope = Tracer.Instance.StartActive("manual.multilang"))
                {
                    var items = line.Split(',');

                    var name = items[3];
                    parentScope.Span.SetTag("manual.multilang.lang", items[0]);
                    parentScope.Span.SetTag("manual.multilang.name", name);

                    var query = $"SELECT * FROM {items[1]} WHERE {items[1]}.{items[2].Replace(" ", "")} = '{name}'";
                    await QueryItemsAsync(query, line);
                }
            }
        }

        private async Task QueryItemsAsync(string query, string line = null)
        {
            try
            {
                Console.WriteLine($"{DateTime.Now:o}: container.GetItemQueryStreamIterator({query})");
                await ExecAndIterateQueryAsync(() => container.GetItemQueryStreamIterator(query));
            }
            catch (Exception ex)
            {
                if (line != null)
                {
                    Console.WriteLine("Error processing line: " + line);
                }
                else
                {
                    Console.WriteLine("Error processing query: " + query);
                }
                Console.WriteLine(ex);
            }
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
