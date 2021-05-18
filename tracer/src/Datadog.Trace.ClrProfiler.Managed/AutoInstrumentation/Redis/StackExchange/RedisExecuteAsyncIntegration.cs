// <copyright file="RedisExecuteAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// StackExchange.Redis.[RedisBase/RedisBatch/RedisTransaction].ExecuteAsync[T] calltarget instrumentation
    /// </summary>
    [RedisExecuteAsyncInstrumentMethod(TypeName = "StackExchange.Redis.RedisBase")]
    [RedisExecuteAsyncInstrumentMethod(TypeName = "StackExchange.Redis.RedisBatch")]
    [RedisExecuteAsyncInstrumentMethod(TypeName = "StackExchange.Redis.RedisTransaction")]
    public class RedisExecuteAsyncIntegration
    {
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.StackExchangeRedis));

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
            where TTarget : IRedisBase
            where TMessage : IMessageData
        {
            string rawCommand = message.CommandAndKey ?? "COMMAND";
            StackExchangeRedisHelper.HostAndPort hostAndPort = StackExchangeRedisHelper.GetHostAndPort(instance.Multiplexer.Configuration);

            Scope scope = RedisHelper.CreateScope(Tracer.Instance, IntegrationId, hostAndPort.Host, hostAndPort.Port, rawCommand);
            if (scope is not null)
            {
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
