using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Core;
using Couchbase.Core.Exceptions;

namespace Samples.Couchbase3
{
    internal class Program
    {
        private static bool ContainsIgnorableException(Exception ex) => ex switch
        {
            AuthenticationException or AuthenticationFailureException => true,
            UnambiguousTimeoutException => true,
            AggregateException aggEx => aggEx.InnerExceptions.Any(ContainsIgnorableException),
            { InnerException: { } inner } => ContainsIgnorableException(inner),
            _ => false,
        };

        private static async Task<int> Main()
        {
            var options = new ClusterOptions() 
                      .WithConnectionString("couchbase://" + ConnectionString())
                      .WithCredentials(username: Username(), password: Password())
                      .WithBuckets(BucketName());


            ICluster cluster = null;

            try
            {
                cluster = await Cluster.ConnectAsync(options);
                await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(15));
            }
            catch (Exception ex) when (ContainsIgnorableException(ex))
            {
                Console.WriteLine("Exception during execution " + ex);
                Console.WriteLine("Exiting with skip code (13)");
                return 13;
            }

            // get a bucket reference
            var bucket = await cluster.BucketAsync(BucketName());

            // get the default collection reference
            var collection = bucket.DefaultCollection();

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

        private static string ConnectionString() => Environment.GetEnvironmentVariable("COUCHBASE_CONNECTION_STRING") ?? "localhost";

        private static string Username() => Environment.GetEnvironmentVariable("COUCHBASE_USERNAME") ?? "default";

        private static string Password() => Environment.GetEnvironmentVariable("COUCHBASE_PASSWORD") ?? "password";

        private static string BucketName() => Environment.GetEnvironmentVariable("COUCHBASE_BUCKET") ?? "default";
    }
}
