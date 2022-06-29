// <copyright file="ConnectionMultiplexerExecuteSyncImplIntegration_2_6_45.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
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
        ParameterTypeNames = new[] { "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "StackExchange.Redis.ServerEndPoint", "!!0" },
        MinimumVersion = "2.0.0", // 2.6.45, but dll uses 2.0.0
        MaximumVersion = "2.*.*",
        IntegrationName = StackExchangeRedisHelper.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = "StackExchange.Redis.StrongName",
        TypeName = "StackExchange.Redis.ConnectionMultiplexer",
        MethodName = "ExecuteSyncImpl",
        ReturnTypeName = "T",
        ParameterTypeNames = new[] { "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "StackExchange.Redis.ServerEndPoint", "!!0" },
        MinimumVersion = "2.0.0", // 2.6.45, but dll uses 2.0.0
        MaximumVersion = "2.*.*",
        IntegrationName = StackExchangeRedisHelper.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class ConnectionMultiplexerExecuteSyncImplIntegration_2_6_45
    {
        internal static CallTargetState OnMethodBegin<TTarget, TMessage, TProcessor, TServerEndPoint, TDefaultValue>(TTarget instance, TMessage message, TProcessor resultProcessor, TServerEndPoint serverEndPoint, TDefaultValue defaultValue)
            where TTarget : IConnectionMultiplexer
            where TMessage : IMessageData
        {
            string rawCommand = message.CommandAndKey ?? "COMMAND";
            StackExchangeRedisHelper.HostAndPort hostAndPort = StackExchangeRedisHelper.GetHostAndPort(instance.Configuration);

            Scope scope = RedisHelper.CreateScope(Tracer.Instance, StackExchangeRedisHelper.IntegrationId, hostAndPort.Host, hostAndPort.Port, rawCommand);
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
        internal static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
