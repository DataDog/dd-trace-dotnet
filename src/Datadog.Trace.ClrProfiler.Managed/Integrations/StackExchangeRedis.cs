using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Wraps calls to the Stack Exchange redis library.
    /// </summary>
    public static class StackExchangeRedis
    {
        private static ConcurrentDictionary<Type, Func<object, object, object, object, object>> _executeSyncImplBoundMethods = new ConcurrentDictionary<Type, Func<object, object, object, object, object>>();

        /// <summary>
        /// Execute a synchronous redis operation.
        /// </summary>
        /// <param name="multiplexer">The multiplexer running the command</param>
        /// <param name="message">The message to send to redis</param>
        /// <param name="processor">The processor to handle the reuslt</param>
        /// <param name="server">The server to call</param>
        /// <returns>The result</returns>
        public static object ExecuteSyncImpl(object multiplexer, object message, object processor, object server)
        {
            var resultType = GetResultTypeFromProcessor(processor);
            if (!_executeSyncImplBoundMethods.TryGetValue(resultType, out var originalMethod))
            {
                var asm = multiplexer.GetType().Assembly;
                var multiplexerType = asm.GetType("StackExchange.Redis.ConnectionMultiplexer");
                var messageType = asm.GetType("StackExchange.Redis.Message");
                var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(resultType);
                var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

                originalMethod = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, object, object, object, object>>(
                    multiplexerType,
                    "ExecuteSyncImpl",
                    new Type[] { messageType, processorType, serverType },
                    new Type[] { resultType });
                _executeSyncImplBoundMethods[resultType] = originalMethod;
            }

            return originalMethod(multiplexer, message, processor, server);
        }

        private static Type GetResultTypeFromProcessor(object processor)
        {
            var type = processor.GetType();
            while (type != null)
            {
                if (type.GenericTypeArguments.Length > 0)
                {
                    return type.GenericTypeArguments[0];
                }

                type = type.BaseType;
            }

            return typeof(object);
        }
    }
}
