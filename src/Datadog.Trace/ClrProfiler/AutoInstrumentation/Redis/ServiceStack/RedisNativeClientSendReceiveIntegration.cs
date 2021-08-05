// <copyright file="RedisNativeClientSendReceiveIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.ServiceStack
{
    /// <summary>
    /// ServiceStack.Redis.RedisNativeClient.SendReceive[T] calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "ServiceStack.Redis",
        TypeName = "ServiceStack.Redis.RedisNativeClient",
        MethodName = "SendReceive",
        ReturnTypeName = "T",
        ParameterTypeNames = new[] { "System.Byte[][]", "System.Func`1[!!0]", "System.Action`1[System.Func`1[!!0]]", ClrNames.Bool },
        MinimumVersion = "4.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = IntegrationName)]
    public class RedisNativeClientSendReceiveIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.ServiceStackRedis);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TFunc">Type of the result processor</typeparam>
        /// <typeparam name="TAction">Type of the server end point</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="cmdWithBinaryArgs">Cmd with binary args</param>
        /// <param name="fn">Function instance</param>
        /// <param name="completePipelineFn">Complete pipeline function instance</param>
        /// <param name="sendWithoutRead">Send without read boolean</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TFunc, TAction>(TTarget instance, byte[][] cmdWithBinaryArgs, TFunc fn, TAction completePipelineFn, bool sendWithoutRead)
            where TTarget : IRedisNativeClient
        {
            Scope scope = RedisHelper.CreateScope(Tracer.Instance, IntegrationId, instance.Host ?? string.Empty, instance.Port.ToString(CultureInfo.InvariantCulture), GetRawCommand(cmdWithBinaryArgs));
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
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }

        private static string GetRawCommand(byte[][] cmdWithBinaryArgs)
        {
            return string.Join(
                " ",
                cmdWithBinaryArgs.Select(
                    bs =>
                    {
                        try
                        {
                            return Encoding.UTF8.GetString(bs);
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    }));
        }
    }
}
