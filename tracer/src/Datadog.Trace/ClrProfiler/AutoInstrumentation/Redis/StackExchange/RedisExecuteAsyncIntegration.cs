// <copyright file="RedisExecuteAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// StackExchange.Redis.[RedisBase/RedisBatch/RedisTransaction].ExecuteAsync[T] calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new string[] { "StackExchange.Redis", "StackExchange.Redis.StrongName" },
        MethodName = "ExecuteAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<!!0>",
        ParameterTypeNames = new[] { "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "StackExchange.Redis.ServerEndPoint" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "2.*.*",
        TypeNames = new[] { "StackExchange.Redis.RedisBase", "StackExchange.Redis.RedisBatch", "StackExchange.Redis.RedisTransaction" },
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RedisExecuteAsyncIntegration
    {
        private const string IntegrationName = nameof(Configuration.IntegrationId.StackExchangeRedis);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.StackExchangeRedis;

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
        internal static CallTargetState OnMethodBegin<TTarget, TMessage, TProcessor, TServerEndPoint>(TTarget instance, TMessage message, TProcessor resultProcessor, TServerEndPoint serverEndPoint)
            where TTarget : IRedisBase
            where TMessage : IMessageData, IDuckType
        {
            if (message.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            string rawCommand = message.CommandAndKey ?? "COMMAND";
            var hostAndPort = StackExchangeRedisHelper.GetHostAndPort(instance.Multiplexer.Configuration);

            var scope = RedisHelper.CreateScope(
                Tracer.Instance,
                IntegrationId,
                IntegrationName,
                hostAndPort.Host,
                hostAndPort.Port,
                rawCommand,
                StackExchangeRedisHelper.GetDb(message.Db));

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
