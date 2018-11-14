using System;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    /// <summary>
    /// Traces StackExchange.Redis.RedisBatch
    /// </summary>
    public class RedisBatch : Base
    {
        /// <summary>
        /// Execute an asynchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="this">The object this method is called on</param>
        /// <param name="message">The message</param>
        /// <param name="processor">The result processor</param>
        /// <param name="server">The server</param>
        /// <returns>An asynchronous task.</returns>
        public static object ExecuteAsync<T>(object @this, object message, object processor, object server)
        {
            return ExecuteAsyncInternal<T>(@this, message, processor, server);
        }

        /// <summary>
        /// Execute an asynchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="this">The object this method is called on</param>
        /// <param name="message">The message</param>
        /// <param name="processor">The result processor</param>
        /// <param name="server">The server</param>
        /// <returns>An asynchronous task.</returns>
        private static async Task<T> ExecuteAsyncInternal<T>(object @this, object message, object processor, object server)
        {
            var thisType = @this.GetType();
            var genericType = typeof(T);
            var asm = thisType.Assembly;
            var batchType = asm.GetType("StackExchange.Redis.RedisBatch");
            var messageType = asm.GetType("StackExchange.Redis.Message");
            var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(genericType);
            var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

            var originalMethod = DynamicMethodBuilder<Func<object, object, object, object, Task<T>>>
               .CreateMethodCallDelegate(
                    thisType,
                    "ExecuteAsync",
                    new[] { messageType, processorType, serverType },
                    new[] { genericType });

            // we only trace RedisBatch methods here
            if (thisType == batchType)
            {
                using (var scope = CreateScope(@this, message))
                {
                    try
                    {
                        return await originalMethod(@this, message, processor, server);
                    }
                    catch (Exception ex)
                    {
                        scope.Span.SetException(ex);
                        throw;
                    }
                }
            }

            return await originalMethod(@this, message, processor, server);
        }

        private static Scope CreateScope(object batch, object message)
        {
            var multiplexer = GetMultiplexer(batch);
            var config = GetConfiguration(multiplexer);
            var hostAndPort = GetHostAndPort(config);
            var cmd = GetRawCommand(batch, message);

            return Integrations.Redis.CreateScope(hostAndPort.Item1, hostAndPort.Item2, cmd, finishOnClose: false);
        }
    }
}
