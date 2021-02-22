using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// StackExchange.Redis.ConnectionMultiplexer.ExecuteSyncImpl calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "StackExchange.Redis",
        TypeName = "StackExchange.Redis.ConnectionMultiplexer",
        MethodName = "ExecuteSyncImpl",
        ReturnTypeName = "T",
        ParameterTypeNames = new[] { "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "StackExchange.Redis.ServerEndPoint" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = "StackExchange.Redis.StrongName",
        TypeName = "StackExchange.Redis.ConnectionMultiplexer",
        MethodName = "ExecuteSyncImpl",
        ReturnTypeName = "T",
        ParameterTypeNames = new[] { "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "StackExchange.Redis.ServerEndPoint" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    public class ConnectionMultiplexerExecuteSyncImplIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.StackExchangeRedis);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMessage">Type of the message</typeparam>
        /// <typeparam name="TProcessor">Type of the result processor</typeparam>
        /// <typeparam name="TServerEndPoint">Type of the server end point</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="message">Message instance</param>
        /// <param name="resultProcessor">Result processor instance</param>
        /// <param name="serverEndPoint">Server endpoint instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TMessage, TProcessor, TServerEndPoint>(TTarget instance, TMessage message, TProcessor resultProcessor, TServerEndPoint serverEndPoint)
            where TTarget : IConnectionMultiplexer
            where TMessage : IMessageData
        {
            string rawCommand = message.CommandAndKey ?? "COMMAND";
            StackExchangeRedisHelper.HostAndPort hostAndPort = StackExchangeRedisHelper.GetHostAndPort(instance.Configuration);

            Scope scope = RedisHelper.CreateScope(Tracer.Instance, IntegrationId, hostAndPort.Host, hostAndPort.Port, rawCommand);
            if (scope is not null)
            {
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
