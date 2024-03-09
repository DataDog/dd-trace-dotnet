// <copyright file="BeforeSendReplyIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    /// <summary>
    /// System.ServiceModel.Dispatcher.ImmutableDispatchRuntime.AfterReceiveRequest calltarget instrumentation.
    /// This integration ends the WCF server span when DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED=true.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.ServiceModel",
        TypeName = "System.ServiceModel.Dispatcher.ImmutableDispatchRuntime",
        MethodName = "BeforeSendReply",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.ServiceModel.Dispatcher.MessageRpc&", $"{ClrNames.Exception}&", $"{ClrNames.Bool}&" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = WcfCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class BeforeSendReplyIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMessageRpc">Type of the rpc message</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="rpc">MessageRpc instance</param>
        /// <param name="exception">The exception instance from preparing the message reply</param>
        /// <param name="thereIsAnUnhandledException">A flag indicating whether exception caught from preparing the message reply is unhandled</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TMessageRpc>(TTarget instance, ref TMessageRpc rpc, ref Exception exception, ref bool thereIsAnUnhandledException)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(WcfCommon.IntegrationId) || !Tracer.Instance.Settings.DelayWcfInstrumentationEnabled || WcfCommon.GetCurrentOperationContext is null)
            {
                return CallTargetState.GetDefault();
            }

            var rpcProxy = rpc.DuckCast<MessageRpcStruct>();
            if (((IDuckType?)rpcProxy.OperationContext.RequestContext)?.Instance is object requestContextInstance
                && WcfCommon.Scopes.TryGetValue(requestContextInstance, out var scope))
            {
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
#endif
