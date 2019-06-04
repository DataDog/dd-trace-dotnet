using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    /// <summary>
    /// Wraps calls to the StackExchange redis library.
    /// </summary>
    public static class StackExchangeRedisMessage
    {
        private const string InstrumentedType = "StackExchange.Redis.Message";
        private const string AssemblyName = "StackExchange.Redis";
        private const string AssemblyStrongName = "StackExchange.Redis.StrongName";
        private const string IntegrationName = "StackExchangeRedis";
        private const string Major1 = "1";
        private const string Major2 = "2";
        private static readonly ILog Log = LogProvider.GetLogger(typeof(StackExchangeRedisMessage));

        private static readonly InterceptedMethodAccess<Action<object, object>> SetEnqueuedAccess = new InterceptedMethodAccess<Action<object, object>>();
        private static readonly InterceptedMethodAccess<Action<object>> CompleteAccess = new InterceptedMethodAccess<Action<object>>();

        /// <summary>
        /// Finished redis operation.
        /// </summary>
        /// <param name="message">The Message to send to redis.</param>
        /// <param name="connection">The PhysicalConnection to use to send to redis.</param>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = AssemblyName,
            TargetAssembly = AssemblyName,
            TargetType = InstrumentedType,
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = AssemblyStrongName,
            TargetAssembly = AssemblyStrongName,
            TargetType = InstrumentedType,
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static void SetEnqueued(object message, object connection)
        {
            var resultType = message.GetType();
            // var messageType = asm.GetType("StackExchange.Redis.Message");
            // var processorType = asm.GetType("StackExchange.Redis.ResultProcessor`1").MakeGenericType(resultType);
            // var serverType = asm.GetType("StackExchange.Redis.ServerEndPoint");

            // var originalMethod = Emit.DynamicMethodBuilder<Func<object, object, object, object, T>>
            //    .CreateMethodCallDelegate(
            //         multiplexerType,
            //         "ExecuteSyncImpl",
            //         new[] { messageType, processorType, serverType },
            //         new[] { resultType });

            Action<object, object> originalMethod;

            try
            {
                originalMethod = SetEnqueuedAccess.GetInterceptedMethod(
                    owningType: resultType,
                    intendedType: InstrumentedType,
                    returnType: Interception.VoidType,
                    methodName: nameof(SetEnqueued),
                    generics: Interception.NullTypeArray,
                    parameters: Interception.ParamsToTypes(connection));
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error calling {InstrumentedType}.{nameof(SetEnqueued)}(object connection)", ex);
                throw;
            }

            using (var scope = CreateScope(connection, message))
            {
                try
                {
                    originalMethod(message, connection);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        /// <summary>
        /// Finished redis operation.
        /// </summary>
        /// <param name="message">The message to send to redis.</param>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = AssemblyName,
            TargetAssembly = AssemblyName,
            TargetType = InstrumentedType,
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = AssemblyStrongName,
            TargetAssembly = AssemblyStrongName,
            TargetType = InstrumentedType,
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static void Complete(object message)
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
                    "ExecuteSyncImpl",
                    new[] { messageType, processorType, serverType },
                    new[] { resultType });

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


        private static Scope CreateScope(object multiplexer, object message)
        {
            var config = StackExchangeRedisHelper.GetConfiguration(multiplexer);
            var hostAndPort = StackExchangeRedisHelper.GetHostAndPort(config);
            var rawCommand = StackExchangeRedisHelper.GetRawCommand(multiplexer, message);

            return RedisHelper.CreateScope(Tracer.Instance, IntegrationName, hostAndPort.Item1, hostAndPort.Item2, rawCommand);
        }
    }
}
