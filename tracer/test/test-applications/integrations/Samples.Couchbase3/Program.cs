using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Core;

namespace Samples.Couchbase3
{
    internal class Program
    {
        private static async Task Main()
        {
            var options = new ClusterOptions() 
                      .WithConnectionString("couchbase://" + Host())
                      .WithCredentials(username: "default", password: "password")
                      .WithBuckets("default");

            var cluster = await Cluster.ConnectAsync(options);

            // get a bucket reference
            var bucket = await cluster.BucketAsync("default");

            // get a user-defined collection reference
#if COUCHBASE_3_0
            var collection = bucket.DefaultCollection();
#else
            var scope = await bucket.ScopeAsync("tenant_agent_00");
            var collection = await scope.CollectionAsync("users");
#endif

            // Upsert Document
            var upsertResult = await collection.UpsertAsync("my-document-key", new { Name = "Ted", Age = 31 });
            var getResult = await collection.GetAsync("my-document-key");

            Console.WriteLine(getResult.ContentAs<dynamic>());

            // Call the QueryAsync() function on the cluster object and store the result.
            var queryResult = await cluster.QueryAsync<dynamic>("select \"Hello World\" as greeting");

            // Iterate over the rows to access result data and print to the terminal.
            await foreach (var row in queryResult)
            {
                Console.WriteLine(row);
            }

            await collection.RemoveAsync("my-document-key");
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("COUCHBASE_HOST") ?? "localhost";
        }
    }
}
