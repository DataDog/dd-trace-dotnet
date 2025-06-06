using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Core;

namespace Samples.Couchbase3
{
    internal class Program
    {
        private static bool ContainsAuthenticationException(Exception ex) => ex switch
        {
            AuthenticationException => true,
            AggregateException aggEx => aggEx.InnerExceptions.Any(ContainsAuthenticationException),
            { InnerException: { } inner } => ContainsAuthenticationException(inner),
            _ => false,
        };

        private static async Task<int> Main()
        {
            var options = new ClusterOptions() 
                      .WithConnectionString("couchbase://" + Host())
                      .WithCredentials(username: "default", password: "password")
                      .WithBuckets("default");


            ICluster cluster = null;

            try
            {
                cluster = await Cluster.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during execution " + ex);

                if (ContainsAuthenticationException(ex))
                {
                    Console.WriteLine("Exiting with skip code (13)");
                    return 13;
                }
            }

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

            // this should error as it doesn't exist
            try
            {
                await collection.RemoveAsync("does-not-exist");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Expected error removing non-existent key: " + ex);
            }

            return 0;
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("COUCHBASE_HOST") ?? "localhost";
        }
    }
}
