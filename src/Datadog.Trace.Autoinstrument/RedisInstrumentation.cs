using System;
using System.Linq;
using System.Reflection;
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
            Profiler.Instrument(
                stringGet,
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "StringGetBefore"),
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "StringGetAfter"),
                typeof(RedisInstrumentation).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(x => x.Name == "StringGetException"));
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
