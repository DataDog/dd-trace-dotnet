using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    /// <summary>
    /// Wraps calls to the StackExchange redis library.
    /// </summary>
    public static class ConnectionMultiplexer
    {
        private const string IntegrationName = "StackExchangeRedis";
        private const string RedisAssembly = "StackExchange.Redis";
        private const string StrongNameRedisAssembly = "StackExchange.Redis.StrongName";
        private const string ConnectionMultiplexerTypeName = "StackExchange.Redis.ConnectionMultiplexer";
        private const string Major1 = "1";
        private const string Major2 = "2";

        // Parameter types
        private const string StackExchangeRedisServerEndPoint = "StackExchange.Redis.ServerEndPoint";
        private const string StackExchangeRedisMessage = "StackExchange.Redis.Message";
        private const string StackExchangeRedisResultProcessorGeneric = "StackExchange.Redis.ResultProcessor`1<T>";
        private const string StackExchangeRedisResultProcessor = "StackExchange.Redis.ResultProcessor`1";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(ConnectionMultiplexer));

        /// <summary>
        /// Execute a synchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="multiplexer">The connection multiplexer running the command.</param>
        /// <param name="message">The message to send to redis.</param>
        /// <param name="processor">The processor to handle the result.</param>
        /// <param name="server">The server to call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The result</returns>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = RedisAssembly,
            TargetAssembly = RedisAssembly,
            TargetType = ConnectionMultiplexerTypeName,
            TargetMethod = "ExecuteSyncImpl",
            TargetSignatureTypes = new[] { "T", StackExchangeRedisMessage, StackExchangeRedisResultProcessorGeneric, StackExchangeRedisServerEndPoint },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = StrongNameRedisAssembly,
            TargetAssembly = StrongNameRedisAssembly,
            TargetType = ConnectionMultiplexerTypeName,
            TargetMethod = "ExecuteSyncImpl",
            TargetSignatureTypes = new[] { "T", StackExchangeRedisMessage, StackExchangeRedisResultProcessorGeneric, StackExchangeRedisServerEndPoint },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static T ExecuteSyncImpl<T>(
            object multiplexer,
            object message,
            object processor,
            object server,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (multiplexer == null)
            {
                throw new ArgumentNullException(nameof(multiplexer));
            }

            var genericType = typeof(T);
            var multiplexerType = multiplexer.GetInstrumentedType(ConnectionMultiplexerTypeName);
            Func<object, object, object, object, T> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, object, object, T>>
                        .Start(moduleVersionPtr, mdToken, opCode, nameof(ExecuteSyncImpl))
                        .WithConcreteType(multiplexerType)
                        .WithParameters(message, processor, server)
                        .WithMethodGenerics(genericType)
                        .WithNamespaceAndNameFilters(
                            ClrNames.Ignore,
                            StackExchangeRedisMessage,
                            StackExchangeRedisResultProcessor,
                            StackExchangeRedisServerEndPoint)
                        .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: ConnectionMultiplexerTypeName,
                    methodName: nameof(ExecuteSyncImpl),
                    instanceType: multiplexer.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScope(multiplexer, message))
            {
                try
                {
                    return instrumentedMethod(multiplexer, message, processor, server);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
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
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>An asynchronous task.</returns>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = RedisAssembly,
            TargetAssembly = RedisAssembly,
            TargetType = ConnectionMultiplexerTypeName,
            TargetMethod = "ExecuteAsyncImpl",
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", StackExchangeRedisMessage, StackExchangeRedisResultProcessorGeneric, ClrNames.Object, StackExchangeRedisServerEndPoint },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = StrongNameRedisAssembly,
            TargetAssembly = StrongNameRedisAssembly,
            TargetType = ConnectionMultiplexerTypeName,
            TargetMethod = "ExecuteAsyncImpl",
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", StackExchangeRedisMessage, StackExchangeRedisResultProcessorGeneric, ClrNames.Object, StackExchangeRedisServerEndPoint },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object ExecuteAsyncImpl<T>(
            object multiplexer,
            object message,
            object processor,
            object state,
            object server,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (multiplexer == null)
            {
                throw new ArgumentNullException(nameof(multiplexer));
            }

            var genericType = typeof(T);
            var multiplexerType = multiplexer.GetInstrumentedType(ConnectionMultiplexerTypeName);
            Func<object, object, object, object, object, Task<T>> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, object, object, object, Task<T>>>
                        .Start(moduleVersionPtr, mdToken, opCode, nameof(ExecuteAsyncImpl))
                        .WithConcreteType(multiplexerType)
                        .WithParameters(message, processor, state, server)
                        .WithMethodGenerics(genericType)
                        .WithNamespaceAndNameFilters(
                            ClrNames.GenericTask,
                            StackExchangeRedisMessage,
                            StackExchangeRedisResultProcessor,
                            ClrNames.Object,
                            StackExchangeRedisServerEndPoint)
                        .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: ConnectionMultiplexerTypeName,
                    methodName: nameof(ExecuteAsyncImpl),
                    instanceType: multiplexer.GetType().AssemblyQualifiedName);
                throw;
            }

            return ExecuteAsyncImplInternal<T>(multiplexer, message, processor, state, server, instrumentedMethod);
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
        /// <param name="originalMethod">The original method.</param>
        /// <returns>An asynchronous task.</returns>
        private static async Task<T> ExecuteAsyncImplInternal<T>(
            object multiplexer,
            object message,
            object processor,
            object state,
            object server,
            Func<object, object, object, object, object, Task<T>> originalMethod)
        {
            using (var scope = CreateScope(multiplexer, message))
            {
                try
                {
                    return await originalMethod(multiplexer, message, processor, state, server).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
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
