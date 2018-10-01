using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        /// <param name="obj">The this object</param>
        /// <param name="message">The message</param>
        /// <param name="processor">The result processor</param>
        /// <param name="server">The server</param>
        /// <returns>An asynchronous task.</returns>
        public static object ExecuteAsync<T>(object obj, object message, object processor, object server)
        {
            var resultType = typeof(Task<T>);
            var asm = obj.GetType().Assembly;
            var batchType = asm.GetType("StackExchange.Redis.RedisBatch");
            var messageType = asm.GetType("StackExchange.Redis.Message");
            var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(typeof(T));
            var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

            var originalMethod = DynamicMethodBuilder<Func<object, object, object, object, Task<T>>>.CreateMethodCallDelegate(
                obj.GetType(),
                "ExecuteAsync",
                new Type[] { messageType, processorType, serverType },
                new Type[] { resultType });

            // we only trace RedisBatch methods here
            if (obj.GetType() == batchType)
            {
                using (var scope = CreateScope(obj, message, server))
                {
                    return scope.Span.Trace(() => originalMethod(obj, message, processor, server));
                }
            }
            else
            {
                return originalMethod(obj, message, processor, server);
            }
        }

        private static Scope CreateScope(object batch, object message, object server)
        {
            var multiplexer = GetMultiplexer(batch);
            var config = GetConfiguration(multiplexer);
            var hostAndPort = GetHostAndPort(config);
            var cmd = GetRawCommand(batch, message);
            return Datadog.Trace.ClrProfiler.Integrations.Redis.CreateScope(hostAndPort.Item1, hostAndPort.Item2, cmd, finishOnClose: false);
        }
    }
}
