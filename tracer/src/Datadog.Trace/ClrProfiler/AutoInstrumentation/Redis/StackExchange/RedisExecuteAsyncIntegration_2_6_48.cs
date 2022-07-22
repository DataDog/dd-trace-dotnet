// <copyright file="RedisExecuteAsyncIntegration_2_6_48.cs" company="Datadog">
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
    /// StackExchange.Redis.[RedisBatch/RedisTransaction].ExecuteAsync[T] calltarget instrumentation for 2.6.48+
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new[] { "StackExchange.Redis", "StackExchange.Redis.StrongName" },
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<T>",
        ParameterTypeNames = new[] { "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "!!0", "StackExchange.Redis.ServerEndPoint" },
        MinimumVersion = "2.0.0",  // 2.6.48, but dll uses 2.0.0
        MaximumVersion = "2.*.*",
        TypeNames = new[] { "StackExchange.Redis.RedisBatch", "StackExchange.Redis.RedisTransaction" },
        IntegrationName = StackExchangeRedisHelper.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class RedisExecuteAsyncIntegration_2_6_48
    {
        internal static CallTargetState OnMethodBegin<TTarget, TMessage, TDefaultValue, TProcessor, TServerEndPoint>(TTarget instance, TMessage message, TDefaultValue defaultValue, TProcessor resultProcessor, TServerEndPoint serverEndPoint)
            where TTarget : IRedisBase
            where TMessage : IMessageData
        {
            string rawCommand = message.CommandAndKey ?? "COMMAND";
            StackExchangeRedisHelper.HostAndPort hostAndPort = StackExchangeRedisHelper.GetHostAndPort(instance.Multiplexer.Configuration);

            Scope scope = RedisHelper.CreateScope(Tracer.Instance, StackExchangeRedisHelper.IntegrationId, hostAndPort.Host, hostAndPort.Port, rawCommand);
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
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
