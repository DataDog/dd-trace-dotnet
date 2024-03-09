// <copyright file="AfterReceiveRequestIntegration.cs" company="Datadog">
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
    /// This integration starts the WCF server span when DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED=true.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.ServiceModel",
        TypeName = "System.ServiceModel.Dispatcher.ImmutableDispatchRuntime",
        MethodName = "AfterReceiveRequest",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.ServiceModel.Dispatcher.MessageRpc&" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = WcfCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AfterReceiveRequestIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMessageRpc">Type of the rpc message</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="rpc">MessageRpc instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TMessageRpc>(TTarget instance, ref TMessageRpc rpc)
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(WcfCommon.IntegrationId) || !tracer.Settings.DelayWcfInstrumentationEnabled || WcfCommon.GetCurrentOperationContext is null)
            {
                return CallTargetState.GetDefault();
            }

            // First, capture the active scope
            var activeScope = tracer.InternalActiveScope;
            var spanContextRaw = DistributedTracer.Instance.GetSpanContextRaw() ?? activeScope?.Span?.Context;

            var rpcProxy = rpc.DuckCast<MessageRpcStruct>();
            var useWcfWebHttpResourceNames = tracer.Settings.WcfWebHttpResourceNamesEnabled;
            var scope = WcfCommon.CreateScope(rpcProxy.Request, useWcfWebHttpResourceNames);
            if (scope is not null
                && ((IDuckType?)rpcProxy.OperationContext.RequestContext)?.Instance is object requestContextInstance)
            {
                WcfCommon.Scopes.Add(requestContextInstance, scope);
            }

            return new CallTargetState(scope, activeScope, spanContextRaw);
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
            // Add an exception and close the span only if there was an uncaught exception,
            // which should only happen if one of the IClientMessageInspector's has thrown an exception.
            // Otherwise, leave the span open and it will be closed after the IDispatchMessageInspector's run.
            if (exception is not null)
            {
                state.Scope.DisposeWithException(exception);
            }

            if (state.Scope != null)
            {
                // OnMethodBegin started an active span that can be accessed by IDispatchMessageInspector's and the actual WCF endpoint
                // Before returning, we must reset the scope to the previous active scope, so that callers of this method do not see this scope
                // Don't worry, this will be accessed and closed by the BeforeSendReplyIntegration
                if (Tracer.Instance.ScopeManager is IScopeRawAccess rawAccess)
                {
                    rawAccess.Active = state.PreviousScope;
                    DistributedTracer.Instance.SetSpanContext(state.PreviousDistributedSpanContext);
                }
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
#endif
