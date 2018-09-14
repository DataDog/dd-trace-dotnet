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
        private static ConcurrentDictionary<Type, dynamic> _executeSyncImplBoundMethods = new ConcurrentDictionary<Type, dynamic>();

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
            dynamic originalMethod;
            if (!_executeSyncImplBoundMethods.TryGetValue(resultType, out originalMethod))
            {
                var method = multiplexer.GetType().GetMethod("ExecuteSyncImpl", BindingFlags.Instance | BindingFlags.NonPublic);
                var boundMethod = method.MakeGenericMethod(resultType);
                var methodParams = boundMethod.GetParameters();
                var funcType = typeof(Func<,,,,>).MakeGenericType(boundMethod.ReturnType, boundMethod.DeclaringType, methodParams[0].ParameterType, methodParams[1].ParameterType, methodParams[2].ParameterType);

                originalMethod = boundMethod.CreateDelegate(funcType);

                _executeSyncImplBoundMethods[resultType] = originalMethod;
            }

            File.WriteAllLines(
                @"C:\Temp\stack-exchange-redis.trace",
                new string[] { $"multiplexer: {multiplexer}", $"message: {message}", $"processor: {processor}", $"server: {server}" });
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
