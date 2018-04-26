using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.ProfilerLib;
using StackExchange.Redis;

namespace Datadog.Trace.Autoinstrument
{
    /// <summary>
    /// This class provides instrumentation for redis
    /// </summary>
    public static class RedisInstrumentation
    {
        /// <summary>
        /// Instruments redis
        /// </summary>
        public static void Instrument()
        {
            var redisDatabase = typeof(RedisKey).Assembly.GetType("StackExchange.Redis.RedisDatabase");
            var stringGet = redisDatabase.GetMethod("StringGet", new[] { typeof(RedisKey), typeof(CommandFlags) });
            var redisBase = typeof(RedisKey).Assembly.GetType("StackExchange.Redis.RedisBase");
            var executeSync = redisDatabase.GetMethod("ExecuteSync", BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            Console.WriteLine($"Execute sync, isgeneric {executeSync.IsGenericMethod}, isGenericMethodDef: {executeSync.IsGenericMethodDefinition}");
            Profiler.Instrument(
                stringGet,
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "StringGetBefore"),
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "StringGetAfter"),
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "StringGetException"));
            Profiler.Instrument(
                executeSync,
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncBefore"),
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncAfter"),
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncException"));
            /*
            var f = typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "F");
            Profiler.Instrument(
                f,
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncBefore"),
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncAfter"),
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "ExecuteSyncException"));
            Thread.Sleep(1000);
            F(6);
            */
       }

       private static T F<T>(T t)
       {
           var a = (object)t;
           return t;
       }

        private static object ExecuteSyncBefore(object o, object message, object processor, object server)
        {
            var span = Tracer.Instance.StartSpan("Redis.ExecuteSync");
            return span;
        }

        private static object ExecuteSyncAfter(object o, object message, object processor, object server, object context, object ret)
        {
            var span = context as Span;
            span.Finish();
            return ret;
        }

        private static void ExecuteSyncException(object o, object message, object processor, object server, object context, Exception ex)
        {
            var span = context as Span;
            span.SetException(ex);
            span.Finish();
        }

        private static object StringGetBefore(object o, RedisKey key, CommandFlags flags)
        {
            var span = Tracer.Instance.StartSpan("Redis.StringGet");
            span.SetTag("redis.key", key.ToString());
            return span;
        }

        private static RedisValue StringGetAfter(object o, RedisKey key, CommandFlags flags, object context, RedisValue ret)
        {
            var span = context as Span;
            span.Finish();
            return ret;
        }

        private static void StringGetException(object o, RedisKey key, CommandFlags flags, object context, Exception ex)
        {
            var span = context as Span;
            span.SetException(ex);
            span.Finish();
        }
    }
}
