using System;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    /// <summary>
    /// Traces StackExchange.Redis.RedisBatch
    /// </summary>
    public static class RedisBatch
    {
        private const string IntegrationName = "StackExchangeRedis";
        private const string RedisAssembly = "StackExchange.Redis";
        private const string StrongNameRedisAssembly = "StackExchange.Redis.StrongName";
        private const string RedisBaseTypeName = "StackExchange.Redis.RedisBase";
        private const string Major1 = "1";
        private const string Major2 = "2";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(RedisBatch));

        private static Assembly _redisAssembly;
        private static Type _redisBaseType;
        private static Type _batchType;

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
            TargetMethod = "ExecuteAsync",
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1<T>", "StackExchange.Redis.ServerEndPoint" },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = StrongNameRedisAssembly,
            TargetAssembly = StrongNameRedisAssembly,
            TargetType = RedisBaseTypeName,
            TargetMethod = "ExecuteAsync",
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

            var thisType = redisBase.GetType();

            if (_redisAssembly == null)
            {
                // get these only once and cache them,
                // no need for locking, race conditions are not a problem
                _redisAssembly = thisType.Assembly;
                _redisBaseType = _redisAssembly.GetType("StackExchange.Redis.RedisBase");
                _batchType = _redisAssembly.GetType("StackExchange.Redis.RedisBatch");
            }

            Func<object, object, object, object, Task<T>> instrumentedMethod;

            try
            {
                instrumentedMethod = MethodBuilder<Func<object, object, object, object, Task<T>>>
                                        .Start(moduleVersionPtr, mdToken, callOpCode, nameof(ExecuteAsync))
                                        .WithConcreteType(_redisBaseType)
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
                    instanceType: thisType.AssemblyQualifiedName);
                throw;
            }

            // we only trace RedisBatch methods here
            if (thisType == _batchType)
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
            var multiplexer = StackExchangeRedisHelper.GetMultiplexer(batch);
            var config = StackExchangeRedisHelper.GetConfiguration(multiplexer);
            var hostAndPort = StackExchangeRedisHelper.GetHostAndPort(config);
            var cmd = StackExchangeRedisHelper.GetRawCommand(batch, message);

            return RedisHelper.CreateScope(Tracer.Instance, IntegrationName, hostAndPort.Item1, hostAndPort.Item2, cmd);
        }
    }
}
