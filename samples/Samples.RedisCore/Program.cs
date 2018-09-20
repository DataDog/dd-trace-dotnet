using System;
using System.Collections.Generic;
using System.Linq;
using ServiceStack.Redis;
using StackExchange.Redis;

namespace Samples.RedisCore
{
    class Program
    {
        static void Main(string[] args)
        {
            string prefix = "";
            if (args.Length > 1)
            {
                prefix = args[1];
            }

            if (args.Length == 0 || args.Contains("ServiceStack"))
            {
                RunServiceStack(prefix);
            }
            if (args.Length == 0 || args.Contains("StackExchange"))
            {
                RunStackExchange(prefix);
            }
        }

        private static void RunServiceStack(string prefix)
        {
            prefix += "ServiceStack.Redis.";

            Console.WriteLine($"Testing ServiceStack.Redis: {prefix}");
            using (var redisManager = new PooledRedisClientManager())
            using (var redis = (RedisClient)redisManager.GetClient())
            {
                // clear
                redis.Set($"{prefix}INCR", 0);

                RunCommands(new TupleList<string, Func<object>>
                {
                    // test SendExpectCode
                    { "PING", () => redis.Ping() },
                    // test SendExpectComplexResponse
                    { "DDCUSTOM", () => redis.Custom("DDCUSTOM", "COMMAND") },
                    // test SendExpectData
                    { "ECHO", () => redis.Echo("Hello World") },
                    // test SendExpectDeeplyNestedMultiData
                    { "SLOWLOG", () => redis.GetSlowlog(5) },
                    // test SendExpectLong
                    { "INCR", () => redis.Incr($"{prefix}INCR") },
                    // test SendExpectDouble},
                    { "INCR", () => redis.IncrByFloat($"{prefix}INCR", 1.25) },
                    // test SendExpectMultiData
                    { "TIME", () => redis.GetServerTime() },
                    // test SendExpectSuccess
                    { "DB", () => { redis.ChangeDb(0); return ""; } },
                    // test SendWithoutRead
                    // only Shutdown, so we will skip for now
                });
            }
        }

        private static void RunStackExchange(string prefix)
        {
            prefix += "StackExchange.Redis.";

            Console.WriteLine($"Testing StackExchange.Redis {prefix}");
            using (var redis = ConnectionMultiplexer.Connect("localhost"))
            {
                var db = redis.GetDatabase();

                db.StringSet($"{prefix}INCR", "0");

                RunCommands(new TupleList<string, Func<object>>
                {
                    { "PING", () => db.Ping() },
                    { "DDCUSTOM", () => db.Execute("DDCUSTOM", "COMMAND") },
                    { "ECHO", () => db.Execute("ECHO", "Hello World") },
                    { "SLOWLOG", () => db.Execute("SLOWLOG", "GET") },
                    { "INCR", () => db.StringIncrement($"{prefix}INCR") },
                    { "INCR", () => db.StringIncrement($"{prefix}INCR", 1.25) },
                    { "TIME", () => db.Execute("TIME") },
                });
            }
        }

        private static void RunCommands(TupleList<string, Func<object>> commands)
        {
            foreach (var cmd in commands)
            {
                object result;
                try
                {
                    result = cmd.Item2();
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                }
                Console.WriteLine($"{cmd.Item1}: {result}");
            }
        }

        private class TupleList<T1, T2> : List<Tuple<T1, T2>>
        {
            public void Add(T1 item, T2 item2)
            {
                Add(new Tuple<T1, T2>(item, item2));
            }
        }
    }
}
