using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    /// <summary>
    /// Traces StackExchange.Redis.RedisBatch
    /// </summary>
    public static class RedisBatch
    {
        internal const string IntegrationName = nameof(IntegrationIds.StackExchangeRedis);
        private const string RedisAssembly = "StackExchange.Redis";
        private const string StrongNameRedisAssembly = "StackExchange.Redis.StrongName";
        private const string RedisBaseTypeName = "StackExchange.Redis.RedisBase";
        private const string Major1 = "1";
        private const string Major2 = "2";

        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.StackExchangeRedis));

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RedisBatch));

        /// <summary>
        /// Execute an asynchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="redisBase">The object this method is called on</param>
        /// <param name="message">The message</param>
        /// <param name="processor">The result processor</param>
        /// <param name="server">The server</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>An asynchronous task.</returns>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = RedisAssembly,
            TargetAssembly = RedisAssembly,
            TargetType = RedisBaseTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1<T>", "StackExchange.Redis.ServerEndPoint" },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = StrongNameRedisAssembly,
            TargetAssembly = StrongNameRedisAssembly,
            TargetType = RedisBaseTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1<T>", "StackExchange.Redis.ServerEndPoint" },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object ExecuteAsync<T>(
            object redisBase,
            object message,
            object processor,
            object server,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (redisBase == null)
            {
                throw new ArgumentNullException(nameof(redisBase));
            }

            return ExecuteAsyncInternal<T>(redisBase, message, processor, server, opCode, mdToken, moduleVersionPtr);
        }

        /// <summary>
        /// Execute an asynchronous redis operation.
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="redisBase">The object this method is called on</param>
        /// <param name="message">The message</param>
        /// <param name="processor">The result processor</param>
        /// <param name="server">The server</param>
        /// <param name="callOpCode">The <see cref="OpCodeValue"/> used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>An asynchronous task.</returns>
        private static async Task<T> ExecuteAsyncInternal<T>(
            object redisBase,
            object message,
            object processor,
            object server,
            int callOpCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (redisBase == null)
            {
                throw new ArgumentNullException(nameof(redisBase));
            }

            Func<object, object, object, object, Task<T>> instrumentedMethod;

            try
            {
                var instrumentedType = redisBase.GetInstrumentedType(RedisBaseTypeName);
                instrumentedMethod = MethodBuilder<Func<object, object, object, object, Task<T>>>
                                        .Start(moduleVersionPtr, mdToken, callOpCode, nameof(ExecuteAsync))
                                        .WithConcreteType(instrumentedType)
                                        .WithMethodGenerics(typeof(T))
                                        .WithParameters(message, processor, server)
                                        .WithNamespaceAndNameFilters(
                                             ClrNames.GenericTask,
                                             "StackExchange.Redis.Message",
                                             "StackExchange.Redis.ResultProcessor`1",
                                             "StackExchange.Redis.ServerEndPoint")
                                        .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: callOpCode,
                    instrumentedType: RedisBaseTypeName,
                    methodName: nameof(ExecuteAsync),
                    instanceType: redisBase.GetType().AssemblyQualifiedName);
                throw;
            }

            // we only trace RedisBatch methods here
            var thisType = redisBase.GetType();
            var batchType = thisType.Assembly.GetType("StackExchange.Redis.RedisBatch", throwOnError: false);

            if (thisType == batchType)
            {
                using (var scope = CreateScope(redisBase, message))
                {
                    try
                    {
                        return await instrumentedMethod(redisBase, message, processor, server).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        scope?.Span.SetException(ex);
                        throw;
                    }
                }
            }

            return await instrumentedMethod(redisBase, message, processor, server).ConfigureAwait(false);
        }

        private static Scope CreateScope(object batch, object message)
        {
            var multiplexerData = batch.As<BatchData>().Multiplexer;
            var hostAndPort = StackExchangeRedisHelper.GetHostAndPort(multiplexerData.Configuration);
            var rawCommand = message.As<MessageData>().CommandAndKey ?? "COMMAND";

            return RedisHelper.CreateScope(Tracer.Instance, IntegrationId, hostAndPort.Host, hostAndPort.Port, rawCommand);
        }

        /*
         * DuckTyping Types
         */

        /// <summary>
        /// Batch data structure for duck typing
        /// </summary>
        [DuckCopy]
        public struct BatchData
        {
            /// <summary>
            /// Multiplexer data structure
            /// </summary>
            [Duck(Name = "multiplexer", Kind = DuckKind.Field)]
            public MultiplexerData Multiplexer;
        }

        /// <summary>
        /// Multiplexer data structure for duck typing
        /// </summary>
        [DuckCopy]
        public struct MultiplexerData
        {
            /// <summary>
            /// Multiplexer configuration
            /// </summary>
            public string Configuration;
        }

        /// <summary>
        /// Message data structure for duck typing
        /// </summary>
        [DuckCopy]
        public struct MessageData
        {
            /// <summary>
            /// Message command and key
            /// </summary>
            public string CommandAndKey;
        }
    }
}
