using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Wraps calls to the Stack Exchange redis library.
    /// </summary>
    public static class StackExchangeRedis
    {
        private static Func<object, string> _getCommandAndKeyMethod;

        private static Func<object, string> _getConfigurationMethod;

        /// <summary>
        /// Execute a synchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="multiplexer">The connection multiplexer running the command.</param>
        /// <param name="message">The message to send to redis.</param>
        /// <param name="processor">The processor to handle the result.</param>
        /// <param name="server">The server to call.</param>
        /// <returns>The result</returns>
        public static T ExecuteSyncImpl<T>(object multiplexer, object message, object processor, object server)
        {
            var resultType = typeof(T);
            var asm = multiplexer.GetType().Assembly;
            var multiplexerType = asm.GetType("StackExchange.Redis.ConnectionMultiplexer");
            var messageType = asm.GetType("StackExchange.Redis.Message");
            var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(resultType);
            var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

            var originalMethod = DynamicMethodBuilder<Func<object, object, object, object, T>>.CreateMethodCallDelegate(
                multiplexerType,
                "ExecuteSyncImpl",
                new Type[] { messageType, processorType, serverType },
                new Type[] { resultType });

            using (var scope = CreateScope(multiplexer, message, server))
            {
                return originalMethod(multiplexer, message, processor, server);
            }
        }

        /// <summary>
        /// Execute an asynchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="multiplexer">The connection multiplexer running the command.</param>
        /// <param name="message">The message to send to redis.</param>
        /// <param name="processor">The processor to handle the result.</param>
        /// <param name="state">The state to use for the task.</param>
        /// <param name="server">The server to call.</param>
        /// <returns>An asynchronous task.</returns>
        public static object ExecuteAsyncImpl<T>(object multiplexer, object message, object processor, object state, object server)
        {
            var resultType = typeof(Task<T>);
            var asm = multiplexer.GetType().Assembly;
            var multiplexerType = asm.GetType("StackExchange.Redis.ConnectionMultiplexer");
            var messageType = asm.GetType("StackExchange.Redis.Message");
            var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(resultType);
            var stateType = typeof(object);
            var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

            var originalMethod = DynamicMethodBuilder<Func<object, object, object, object, object, Task<T>>>.CreateMethodCallDelegate(
                multiplexerType,
                "ExecuteAsyncImpl",
                new Type[] { messageType, processorType, stateType, serverType },
                new Type[] { resultType });

            using (var scope = CreateScope(multiplexer, message, server, finishOnClose: false))
            {
                return scope.Span.Trace(() => originalMethod(multiplexer, message, processor, state, server));
            }
        }

        private static Scope CreateScope(object multiplexer, object message, object server, bool finishOnClose = true)
        {
            string config = GetConfiguration(multiplexer);
            string host = null;
            string port = null;

            if (config != null)
            {
                // config can contain several settings separated by commas:
                // hostname:port,name=MyName,keepAlive=180,syncTimeout=10000,abortConnect=False
                // split in commas, find the one without '=', split that one on ':'
                string[] hostAndPort = config.Split(',')
                                             .FirstOrDefault(p => !p.Contains("="))
                                            ?.Split(':');

                if (hostAndPort != null)
                {
                    host = hostAndPort[0];
                }

                // check length because port is optional
                if (hostAndPort?.Length > 1)
                {
                    port = hostAndPort[1];
                }
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
                    _getCommandAndKeyMethod = DynamicMethodBuilder<Func<object, string>>.CreateMethodCallDelegate(messageType, "get_CommandAndKey");
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
            try
            {
                if (_getConfigurationMethod == null)
                {
                    _getConfigurationMethod = DynamicMethodBuilder<Func<object, string>>.CreateMethodCallDelegate(multiplexer.GetType(), "get_Configuration");
                }

                return _getConfigurationMethod(multiplexer);
            }
            catch
            {
                return null;
            }
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
