using System;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    /// <summary>
    /// Traces StackExchange.Redis.RedisBatch
    /// </summary>
    public static class RedisBatch
    {
        private const string RedisBaseTypeName = "StackExchange.Redis.RedisBase";
        private const string RedisAssemblyName = "StackExchange.Redis";
        private const string RedisStrongNameAssemblyName = "StackExchange.Redis.StrongName";
        private const string IntegrationName = "StackExchangeRedis";
        private const string Major1 = "1";

        /// <summary>
        /// Execute an asynchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="redisBase">The object this method is called on</param>
        /// <param name="message">The message</param>
        /// <param name="processor">The result processor</param>
        /// <param name="server">The server</param>
        /// <returns>An asynchronous task.</returns>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = RedisAssemblyName,
            TargetAssembly = RedisAssemblyName,
            TargetType = RedisBaseTypeName,
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major1)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = RedisStrongNameAssemblyName,
            TargetAssembly = RedisStrongNameAssemblyName,
            TargetType = RedisBaseTypeName,
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major1)]
        public static object ExecuteAsync<T>(object redisBase, object message, object processor, object server)
        {
            return ExecuteAsyncInternal<T>(redisBase, message, processor, server);
        }

        /// <summary>
        /// Execute an asynchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="redisBase">The object this method is called on</param>
        /// <param name="message">The message</param>
        /// <param name="processor">The result processor</param>
        /// <param name="server">The server</param>
        /// <returns>An asynchronous task.</returns>
        private static async Task<T> ExecuteAsyncInternal<T>(object redisBase, object message, object processor, object server)
        {
            var thisType = redisBase.GetType();
            var genericType = typeof(T);
            var asm = thisType.Assembly;
            var batchType = asm.GetType("StackExchange.Redis.RedisBatch");
            var messageType = asm.GetType("StackExchange.Redis.Message");
            var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(genericType);
            var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

            var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, object, object, Task<T>>>
               .CreateMethodCallDelegate(
                    thisType,
                    "ExecuteAsync",
                    new[] { messageType, processorType, serverType },
                    new[] { genericType });

            // we only trace RedisBatch methods here
            if (thisType == batchType)
            {
                using (var scope = CreateScope(redisBase, message))
                {
                    try
                    {
                        return await originalMethod(redisBase, message, processor, server).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                    {
                        // unreachable code
                        throw;
                    }
                }
            }

            return await originalMethod(redisBase, message, processor, server).ConfigureAwait(false);
        }

        private static Scope CreateScope(object batch, object message)
        {
            var multiplexer = StackExchangeRedisHelper.GetMultiplexer(batch);
            var config = StackExchangeRedisHelper.GetConfiguration(multiplexer);
            var hostAndPort = StackExchangeRedisHelper.GetHostAndPort(config);
            var cmd = StackExchangeRedisHelper.GetRawCommand(batch, message);

            return RedisHelper.CreateScope(Tracer.Instance, IntegrationName, hostAndPort.Item1, hostAndPort.Item2, cmd);
        }
    }
}
