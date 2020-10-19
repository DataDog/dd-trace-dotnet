using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ServiceStack.Redis;

namespace Samples.Samples.ServiceStackRedis
{
    class Program
    {
        static void Main(string[] args)
        {
            string prefix = "";
            if (args.Length > 0)
            {
                prefix = args[0];
            }

            RunServiceStack(prefix);
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("SERVICESTACK_REDIS_HOST") ?? "localhost:6379";
        }

        private static void RunServiceStack(string prefix)
        {
            prefix += "ServiceStack.Redis.";

            Console.WriteLine($"Testing ServiceStack.Redis: {prefix}");
            using (var redisManager = new PooledRedisClientManager(Host()))
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
        private static object TaskResult(Task task)
        {
            var taskType = task.GetType();

            bool isTaskOfT =
                taskType.IsGenericType
                && taskType.GetGenericTypeDefinition() == typeof(Task<>);

            if (isTaskOfT)
            {
                return taskType.GetProperty("Result").GetValue(task);
            }
            else
            {
                task.Wait();
                return null;
            }
        }

        private static void RunCommands(TupleList<string, Func<object>> commands)
        {
            foreach (var cmd in commands)
            {
                var f = cmd.Item2;
                if (f == null)
                {
                    continue;
                }

                object result;
                try
                {
                    result = f();
                }
                catch (Exception e)
                {
                    while (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }
                    result = e.Message;
                }

                if (result is Task task)
                {
                    result = TaskResult(task);
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
