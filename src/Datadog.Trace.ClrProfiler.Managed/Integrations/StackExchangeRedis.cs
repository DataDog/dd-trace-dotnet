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
        private static ConcurrentDictionary<Type, Func<object, object, object, object, object>> _executeSyncImplBoundMethods =
            new ConcurrentDictionary<Type, Func<object, object, object, object, object>>();

        private static ConcurrentDictionary<Type, Func<object, object, object, object, object, object>> _executeAsyncImplBoundMethods =
            new ConcurrentDictionary<Type, Func<object, object, object, object, object, object>>();

        private static Func<object, string> _getCommandAndKeyMethod;

        private static Func<object, string> _getConfigurationMethod;

        /// <summary>
        /// Execute a synchronous redis operation.
        /// </summary>
        /// <param name="multiplexer">The connection multiplexer running the command.</param>
        /// <param name="message">The message to send to redis.</param>
        /// <param name="processor">The processor to handle the result.</param>
        /// <param name="server">The server to call.</param>
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

            using (var scope = CreateScope(multiplexer, message, server))
            {
                return originalMethod(multiplexer, message, processor, server);
            }
        }

        /// <summary>
        /// Execute an asynchronous redis operation.
        /// </summary>
        /// <param name="multiplexer">The connection multiplexer running the command.</param>
        /// <param name="message">The message to send to redis.</param>
        /// <param name="processor">The processor to handle the result.</param>
        /// <param name="state">The state to use for the task.</param>
        /// <param name="server">The server to call.</param>
        /// <returns>An asynchronous task.</returns>
        public static object ExecuteAsyncImpl(object multiplexer, object message, object processor, object state, object server)
        {
            var resultType = GetResultTypeFromProcessor(processor);
            if (!_executeAsyncImplBoundMethods.TryGetValue(resultType, out var originalMethod))
            {
                var asm = multiplexer.GetType().Assembly;
                var multiplexerType = asm.GetType("StackExchange.Redis.ConnectionMultiplexer");
                var messageType = asm.GetType("StackExchange.Redis.Message");
                var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(resultType);
                var stateType = typeof(object);
                var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

                originalMethod = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, object, object, object, object, object>>(
                    multiplexerType,
                    "ExecuteAsyncImpl",
                    new Type[] { messageType, processorType, stateType, serverType },
                    new Type[] { resultType });
                _executeAsyncImplBoundMethods[resultType] = originalMethod;
            }

            using (var scope = CreateScope(multiplexer, message, server, finishOnClose: false))
            {
                try
                {
                    var result = originalMethod(multiplexer, message, processor, state, server);
                    if (result is Task task)
                    {
                        task.ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                scope.Span.SetException(t.Exception);
                                scope.Span.Finish();
                            }
                            else if (t.IsCanceled)
                            {
                                // abandon the span
                            }
                            else
                            {
                                scope.Span.Finish();
                            }
                        });
                    }
                    else
                    {
                        scope.Span.Finish();
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    scope.Span.Finish();
                    throw;
                }
            }
        }

        private static Scope CreateScope(object multiplexer, object message, object server, bool finishOnClose = true)
        {
            var config = GetConfiguration(multiplexer);
            var host = config;
            var port = "6379";
            if (host.Contains(":"))
            {
                port = host.Substring(host.IndexOf(':') + 1);
                host = host.Substring(0, host.IndexOf(':'));
            }

            var rawCommand = GetRawCommand(multiplexer, message);
            return Redis.CreateScope(host, port, rawCommand, finishOnClose);
        }

        private static string GetRawCommand(object multiplexer, object message)
        {
            string cmdAndKey = null;
            try
            {
                if (_getCommandAndKeyMethod == null)
                {
                    var asm = multiplexer.GetType().Assembly;
                    var messageType = asm.GetType("StackExchange.Redis.Message");
                    _getCommandAndKeyMethod = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, string>>(messageType, "get_CommandAndKey");
                }

                cmdAndKey = _getCommandAndKeyMethod(message);
            }
            catch
            {
            }

            return cmdAndKey ?? "COMMAND";
        }

        private static string GetConfiguration(object multiplexer)
        {
            string config = null;
            try
            {
                if (_getConfigurationMethod == null)
                {
                    _getConfigurationMethod = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, string>>(multiplexer.GetType(), "get_Configuration");
                }

                config = _getConfigurationMethod(multiplexer);
            }
            catch
            {
            }

            return config ?? "localhost:6379";
        }

        /// <summary>
        /// Processor is a ResultProcessor&lt;T&gt;. This method returns the type of T.
        /// </summary>
        /// <param name="processor">The result processor</param>
        /// <returns>The generic type</returns>
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
