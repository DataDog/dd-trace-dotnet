using System;
using System.Threading;
using System.Threading.Tasks;
using Aerospike.Client;

namespace Samples.Aerospike
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host();

            var client = new AsyncClient(host.Item1, host.Item2);

            var key1 = new Key("test", "myset1", "mykey1");
            var key2 = new Key("test", "myset2", "mykey2");
            var key3 = new Key("test", "myset3", "mykey3");

            var bin1 = new Bin("name", "John");
            var bin2 = new Bin("age", 25);
            var bin3 = new Bin("hello", "world");
            var bin4 = new Bin("first", "first");
            var bin5 = new Bin("first", "last");

            // Synchronous methods
            client.Put(null, key1, bin1, bin2);
            client.Add(null, key1, bin3);
            client.Prepend(null, key1, bin4);
            client.Append(null, key1, bin5);

            _ = client.Get(null, key1);
            _ = client.Exists(null, key1);
            _ = client.Get(null, new[] { key1, key2, key3 });
            _ = client.Exists(null, new[] { key1, key2, key3 });

            client.CreateIndex(null, "test", "myset1", indexName: "age", binName: "age", IndexType.NUMERIC).Wait();

            var statement = new Statement
            {
                Namespace = "test",
                SetName = "myset1",
                BinNames = new[] { "name", "age" },
                Filter = Filter.Range("age", 20, 30)
            };

            var result = client.Query(new QueryPolicy(), statement);

            while (result.Next()) ; // Force the query to execute

            client.Delete(null, key1);

            // Asynchronous methods
            await client.Put(null, CancellationToken.None, key1, bin1, bin2);
            await client.Add(null, CancellationToken.None, key1, bin3);
            await client.Prepend(null, CancellationToken.None, key1, bin4);
            await client.Append(null, CancellationToken.None, key1, bin5);

            _ = await client.Get(null, CancellationToken.None, key1);
            _ = await client.Exists(null, CancellationToken.None, key1);
            _ = await client.Get(null, CancellationToken.None, new[] { key1, key2, key3 });
            _ = await client.Exists(null, CancellationToken.None, new[] { key1, key2, key3 });

            await client.Delete(null, CancellationToken.None, key1);

            client.DropIndex(null, "test", "myset1", indexName: "age").Wait();

            client.Close();
        }

        private static Tuple<string, int> Host()
        {
            var host = Environment.GetEnvironmentVariable("AEROSPIKE_HOST");

            if (host == null)
            {
                return Tuple.Create("localhost", 3000);
            }

            var values = host.Split(':');

            return Tuple.Create(values[0], int.Parse(values[1]));
        }
    }
}
