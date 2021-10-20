using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;

namespace Samples.Couchbase.V2
{
    internal class Program
    {
        ICluster _cluster;
        IBucket _bucket;

        private static async Task Main()
        {
            Program p = new Program();
            await p.RunAllExamples();
        }

        private async Task RunAllExamples()
        {
            var config = GetConnectionConfig();

            _cluster = new Cluster(config);

            _cluster.Authenticate("Bob", "password"); // doesn't work without authentication on "recent" couchbase server versions. Method available starting v2.7.4
            _bucket = _cluster.OpenBucket("default");
            RetrieveAndUpdate();
            await RetrieveAndUpdateAsync();
        }

        private ClientConfiguration GetConnectionConfig()
        {
            return new ClientConfiguration
            {
                Servers = new List<Uri> {
                    new Uri("http://10.0.2.2:8091/pools")
                },
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                  {
                    { "default", new BucketConfiguration
                    {
                      BucketName = "default",
                      UseSsl = false,
                      DefaultOperationLifespan = 2000,
                      PoolConfiguration = new PoolConfiguration
                      {
                        MaxSize = 10,
                        MinSize = 5,
                        SendTimeout = 12000
                      }
                    }}
                  }
            };
        }

        public void RetrieveAndUpdate()
        {
            var key = "SampleApp-" + DateTime.Now.Ticks;
            var data = new Data
            {
                Number = 42,
                Text = "Life, the Universe, and Everything",
                Date = DateTime.UtcNow
            };

            // Get non-existent document.
            // Note that it's enough to check the Status property,
            // We're only checking all three to show they exist.
            var notFound = _bucket.Get<dynamic>(key);
            if (!notFound.Success &&
                notFound.Status == ResponseStatus.KeyNotFound)
                Console.WriteLine("Document doesn't exist!");

            // Prepare a string value
            _bucket.Upsert(key, "Hello Couchbase!");

            // Get a string value
            var nonDocResult = _bucket.Get<string>(key);
            Console.WriteLine("Found: " + nonDocResult.Value);

            // Prepare a JSON document value
            _bucket.Upsert(key, data);

            // Get a JSON document string value
            var docResult = _bucket.Get<Data>(key);
            Console.WriteLine("Found: " + docResult.Value);

            // Change the data
            data.Number++;
            data.Text = "What's 7 * 6 + 1?";
            data.Date = DateTime.UtcNow;

            // Try to insert under the same key should fail
            var insertResult = _bucket.Insert(key, data);
            if (!insertResult.Success)
                Console.WriteLine("Inserting under an existing key fails as expected.");

            // Replace existing document
            // Note this only works if the key already exists
            var replaceResult = _bucket.Replace(key, data);

            // Check that the data was updated
            var newDocument = _bucket.Get<Data>(key);
            Console.WriteLine("Got: " + data.Text);

            // Check that the data was updated
            var res = _bucket.Remove(key);
            Console.WriteLine("Got: " + res.Status);
        }

        public async Task RetrieveAndUpdateAsync()
        {
            var key = "SampleAppAsync--" + DateTime.Now.Ticks;
            var data = new Data
            {
                Number = 42,
                Text = "Life, the Universe, and Everything",
                Date = DateTime.UtcNow
            };

            // Get non-existent document.
            // Note that it's enough to check the Status property,
            // We're only checking all three to show they exist.
            var notFound = await _bucket.GetAsync<dynamic>(key);
            if (!notFound.Success &&
                notFound.Status == ResponseStatus.KeyNotFound)
                Console.WriteLine("Document doesn't exist!");

            // Prepare a string value
            await _bucket.UpsertAsync(key, "Hello Couchbase!");

            // Get a string value
            var nonDocResult = await _bucket.GetAsync<string>(key);
            Console.WriteLine("Found: " + nonDocResult.Value);

            // Prepare a JSON document value
            await _bucket.UpsertAsync(key, data);

            // Get a JSON document string value
            var docResult = await _bucket.GetAsync<Data>(key);
            Console.WriteLine("Found: " + docResult.Value);

            // Change the data
            data.Number++;
            data.Text = "What's 7 * 6 + 1?";
            data.Date = DateTime.UtcNow;

            // Try to insert under the same key should fail
            var insertResult = await _bucket.InsertAsync(key, data);
            if (!insertResult.Success)
                Console.WriteLine("Inserting under an existing key fails as expected.");

            // Replace existing document
            // Note this only works if the key already exists
            var replaceResult = await _bucket.ReplaceAsync(key, data);

            // Check that the data was updated
            var newDocument = await _bucket.GetAsync<Data>(key);
            Console.WriteLine("Got: " + data.Text);
        }
    }
}
