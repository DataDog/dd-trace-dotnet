using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using ServiceStack.Redis;

namespace Samples.ServiceStackRedis
{
    class Program
    {
        static readonly Type _clientManagerType = typeof(PooledRedisClientManager);

        static void Main(string[] args)
        {
            string prefix = "";
            if (args.Length > 0)
            {
                prefix = args[0];
            }

            // Use the compile-time types and run the tests
            using (var redisManager = new PooledRedisClientManager(Host()))
            using (var redis = (RedisClient)redisManager.GetClient())
            {
                RunServiceStack(prefix, new RedisClientWrapper(redis));
            }

            // Now use LoadFile to load a second instance and re-run the tests
            var loadFileAssembly = Assembly.LoadFile(_clientManagerType.Assembly.Location);

            using (IDisposable redisManager = (IDisposable)InstantiateClientManagerFromAssembly(loadFileAssembly))
            using (IDisposable redis = (IDisposable)GetClientFromClientManager(redisManager))
            {
                RunServiceStack(prefix, new RedisClientWrapper(redis));
            }

#if NETCOREAPP3_1_OR_GREATER
            var alc = new System.Runtime.Loader.AssemblyLoadContext($"NewAssemblyLoadContext");
            var alcAssembly = alc.LoadFromAssemblyPath(typeof(PooledRedisClientManager).Assembly.Location);

            using (IDisposable redisManager = (IDisposable)InstantiateClientManagerFromAssembly(alcAssembly))
            using (IDisposable redis = (IDisposable)GetClientFromClientManager(redisManager))
            {
                RunServiceStack(prefix, new RedisClientWrapper(redis));
            }
#endif
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("SERVICESTACK_REDIS_HOST") ?? "localhost:6379";
        }

        private static void RunServiceStack(string prefix, RedisClientWrapper redis)
        {
            prefix += "ServiceStack.Redis.";

            Console.WriteLine($"Testing ServiceStack.Redis: {prefix}");

            // clear
            redis.ChangeDb(2);
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

        private static object InstantiateClientManagerFromAssembly(Assembly assembly)
        {
            var type = assembly.GetType(_clientManagerType.FullName);
            var constructor = type.GetConstructor(new Type[] { typeof(string[])});
            return constructor.Invoke(new object[] { new string[] { Host() } });
        }

        private static object GetClientFromClientManager(object clientManager) => clientManager.GetType().GetMethod("GetClient").Invoke(clientManager, null);
    }
}
