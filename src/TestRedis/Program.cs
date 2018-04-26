using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.ProfilerLib;
using StackExchange.Redis;
using Datadog.Trace;

namespace TestRedis
{
    class Program
    {
        private static object ExecuteSyncBefore(object o, object message, object processor, object server)
        {
            Console.WriteLine(message.GetType().GetProperty("Command").GetValue(message, null));
            var span = Tracer.Instance.StartSpan("Redis.ExecuteSync");
            return span;
        }

        private static object ExecuteSyncAfter(object o, object message, object processor, object server, object context, object ret)
        {
            Console.WriteLine("Goodbye");
            var span = context as Span;
            span.Finish();
            return ret;
        }

        private static void ExecuteSyncException(object o, object message, object processor, object server, object context, Exception ex)
        {
            /*
            var span = context as Span;
            span.SetException(ex);
            span.Finish();
            */
        }

        static void Main(string[] args)
        {
            var redisBase = typeof(RedisKey).Assembly.GetType("StackExchange.Redis.RedisBase");
            var executeSync = redisBase.GetMethod("ExecuteSync", BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            foreach(var param in executeSync.GetParameters()) {
                Console.WriteLine($"{param.ParameterType}, {param.ParameterType.ContainsGenericParameters}");

            }
            Console.WriteLine($"Execute sync, isgeneric {executeSync.IsGenericMethod}, isGenericMethodDef: {executeSync.IsGenericMethodDefinition}");
            
            Profiler.Instrument(
                executeSync,
                typeof(Program).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncBefore"),
                typeof(Program).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncAfter"),
                typeof(Program).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncException"));
            Console.WriteLine("Hello World!");
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("127.0.0.1");
            IDatabase db = redis.GetDatabase();
            for (int i = 0; i < 100; i++)
            {
                var result = db.StringGet("jkj");
            }

            Thread.Sleep(1000);
        }
    }
}
