using System;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    /// <summary>
    /// Wraps calls to the StackExchange redis library.
    /// </summary>
    public static class ConnectionMultiplexer
    {
        private const string IntegrationName = "StackExchangeRedis";
        private const string Major1 = "1";
        private const string Major2 = "2";

        /// <summary>
        /// Execute a synchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="multiplexer">The connection multiplexer running the command.</param>
        /// <param name="message">The message to send to redis.</param>
        /// <param name="processor">The processor to handle the result.</param>
        /// <param name="server">The server to call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <returns>The result</returns>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = "StackExchange.Redis",
            TargetAssembly = "StackExchange.Redis",
            TargetType = "StackExchange.Redis.ConnectionMultiplexer",
            TargetSignatureTypes = new[] { ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = "StackExchange.Redis.StrongName",
            TargetAssembly = "StackExchange.Redis.StrongName",
            TargetType = "StackExchange.Redis.ConnectionMultiplexer",
            TargetSignatureTypes = new[] { ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static T ExecuteSyncImpl<T>(object multiplexer, object message, object processor, object server, int opCode)
        {
            var resultType = typeof(T);
            var multiplexerType = multiplexer.GetType();
            var asm = multiplexerType.Assembly;
            var messageType = asm.GetType("StackExchange.Redis.Message");
            var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(resultType);
            var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

            var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, object, object, T>>
               .CreateMethodCallDelegate(
                    multiplexerType,
                    methodName: "ExecuteSyncImpl",
                    methodParameterTypes: new[] { messageType, processorType, serverType },
                    methodGenericArguments: new[] { resultType });

            using (var scope = CreateScope(multiplexer, message))
            {
                try
                {
                    return originalMethod(multiplexer, message, processor, server);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
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
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <returns>An asynchronous task.</returns>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = "StackExchange.Redis",
            TargetAssembly = "StackExchange.Redis",
            TargetType = "StackExchange.Redis.ConnectionMultiplexer",
            TargetSignatureTypes = new[] { ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = "StackExchange.Redis.StrongName",
            TargetAssembly = "StackExchange.Redis.StrongName",
            TargetType = "StackExchange.Redis.ConnectionMultiplexer",
            TargetSignatureTypes = new[] { ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore, ClrNames.Ignore },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object ExecuteAsyncImpl<T>(object multiplexer, object message, object processor, object state, object server, int opCode)
        {
            return ExecuteAsyncImplInternal<T>(multiplexer, message, processor, state, server);
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
        private static async Task<T> ExecuteAsyncImplInternal<T>(object multiplexer, object message, object processor, object state, object server)
        {
            var genericType = typeof(T);
            var multiplexerType = multiplexer.GetType();
            var asm = multiplexerType.Assembly;
            var messageType = asm.GetType("StackExchange.Redis.Message");
            var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(genericType);
            var stateType = typeof(object);
            var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

            var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, object, object, object, Task<T>>>
               .CreateMethodCallDelegate(
                    multiplexerType,
                    methodName: "ExecuteAsyncImpl",
                    methodParameterTypes: new[] { messageType, processorType, stateType, serverType },
                    methodGenericArguments: new[] { genericType });

            using (var scope = CreateScope(multiplexer, message))
            {
                try
                {
                    return await originalMethod(multiplexer, message, processor, state, server).ConfigureAwait(false);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        private static Scope CreateScope(object multiplexer, object message)
        {
            var config = StackExchangeRedisHelper.GetConfiguration(multiplexer);
            var hostAndPort = StackExchangeRedisHelper.GetHostAndPort(config);
            var rawCommand = StackExchangeRedisHelper.GetRawCommand(multiplexer, message);

            return RedisHelper.CreateScope(Tracer.Instance, IntegrationName, hostAndPort.Item1, hostAndPort.Item2, rawCommand);
        }
    }
}
