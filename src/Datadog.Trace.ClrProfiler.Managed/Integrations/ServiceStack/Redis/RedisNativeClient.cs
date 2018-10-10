using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace Datadog.Trace.ClrProfiler.Integrations.ServiceStack.Redis
{
    /// <summary>
    /// Wraps a RedisNativeClient.
    /// </summary>
    public static class RedisNativeClient
    {
        /// <summary>
        /// Traces SendReceive.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="redisNativeClient">The redis native client</param>
        /// <param name="cmdWithBinaryArgs">The command with args</param>
        /// <param name="fn">The function</param>
        /// <param name="completePipelineFn">An optional function to call to complete a pipeline</param>
        /// <param name="sendWithoutRead">Whether or to send without waiting for the result</param>
        /// <returns>The original result</returns>
        public static T SendReceive<T>(object redisNativeClient, byte[][] cmdWithBinaryArgs, object fn, object completePipelineFn, bool sendWithoutRead)
        {
            var originalMethod = DynamicMethodBuilder<Func<object, byte[][], object, object, bool, T>>.GetOrCreateMethodCallDelegate(
                redisNativeClient.GetType(), "SendReceive", methodGenericArguments: new Type[] { typeof(T) });

            using (var scope = Integrations.Redis.CreateScope(GetHost(redisNativeClient), GetPort(redisNativeClient), GetRawCommand(cmdWithBinaryArgs)))
            {
                return (T)scope.Span.Trace(() => originalMethod(redisNativeClient, cmdWithBinaryArgs, fn, completePipelineFn, sendWithoutRead));
            }
        }

        private static string GetHost(dynamic redisNativeClient)
        {
            try
            {
                return redisNativeClient?.Host;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetPort(dynamic redisNativeClient)
        {
            try
            {
                return ((int)redisNativeClient?.Port).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetRawCommand(byte[][] cmdWithBinaryArgs)
        {
            return string.Join(" ", cmdWithBinaryArgs.Select(bs =>
            {
                try
                {
                    return Encoding.UTF8.GetString(bs);
                }
                catch
                {
                    return string.Empty;
                }
            }));
        }
    }
}
