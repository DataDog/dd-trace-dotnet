using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;

namespace StackExchange.Redis.StackOverflow
{
    internal class Program
    {
        private static async Task<int> Main()
        {
            try
            {
                Console.WriteLine($"Profiler attached: {Instrumentation.ProfilerAttached}");

                string host = Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_HOST") ?? "localhost:6389";
                const int database = 1;

                using (var redis = ConnectionMultiplexer.Connect($"{host},allowAdmin=true"))
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

            return (int)ExitCode.Success;
        }

        public static IEnumerable<KeyValuePair<RedisKey, RedisValue>> CreateKeyValuePairs()
        {
            int count = 0;

            while (true)
            {
                yield return new KeyValuePair<RedisKey, RedisValue>($"key{count}", $"value{count}");
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
