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

            var key = new Key("test", "myset", "mykey");

            var bin1 = new Bin("name", "John");
            var bin2 = new Bin("age", 25);
            var bin3 = new Bin("hello", "world");
            var bin4 = new Bin("first", "first");
            var bin5 = new Bin("first", "last");

            // Synchronous methods
            client.Put(null, key, bin1, bin2);
            client.Add(null, key, bin3);
            client.Prepend(null, key, bin4);
            client.Append(null, key, bin5);

            _ = client.Get(null, key);
            _ = client.Exists(null, key);

            client.Delete(null, key);

            // Asynchronous methods
            await client.Put(null, CancellationToken.None, key, bin1, bin2);
            await client.Add(null, CancellationToken.None, key, bin3);
            await client.Prepend(null, CancellationToken.None, key, bin4);
            await client.Append(null, CancellationToken.None, key, bin5);

            _ = await client.Get(null, CancellationToken.None, key);
            _ = await client.Exists(null, CancellationToken.None, key);

            await client.Delete(null, CancellationToken.None, key);

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
