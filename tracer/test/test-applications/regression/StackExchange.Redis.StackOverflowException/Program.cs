using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StackExchange.Redis.StackOverflowException
{
    internal class Program
    {
        private static async Task<int> Main()
        {
            try
            {
                Console.WriteLine($"Profiler attached: {Samples.SampleHelpers.IsProfilerAttached()}");

                string host = Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_SINGLE_HOST") ?? "localhost:6389";
                const int database = 1;

                var connectionString = $"{host},allowAdmin=true,abortConnect=false,syncTimeout=10000";
                using (var redis = ConnectionMultiplexer.Connect(connectionString))
                {
                    IServer server = redis.GetServer(host);
                    server.FlushDatabase(database);

                    var keyValuePairs = CreateKeyValuePairs().Take(20).ToArray();
                    RedisKey[] keys = keyValuePairs.Select(pair => pair.Key).ToArray();

                    IDatabase db = redis.GetDatabase(database);
                    await db.StringSetAsync(keyValuePairs);
                    RedisValue[] values = await db.StringGetAsync(keys);

                    foreach (RedisKey key in server.Keys(database, pageSize: 1))
                    {
                        RedisValue value = await db.StringGetAsync(key);
                        Console.WriteLine($"{key}:{value}");
                    }
                }


                Console.WriteLine("No stack overflow exceptions!");
                Console.WriteLine("All is well!");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

#if NETCOREAPP2_1
            // Add a delay to avoid a race condition on shutdown: https://github.com/dotnet/coreclr/pull/22712
            // This would cause a segmentation fault on .net core 2.x
            System.Threading.Thread.Sleep(5000);
#endif

            Console.WriteLine("App completed successfully");
            return (int)ExitCode.Success;
        }

        public static IEnumerable<KeyValuePair<RedisKey, RedisValue>> CreateKeyValuePairs()
        {
            int count = 0;

            while (true)
            {
                yield return new KeyValuePair<RedisKey, RedisValue>($"StackOverflowExceptionKey{count}", $"value{count}");
                count++;
            }
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
