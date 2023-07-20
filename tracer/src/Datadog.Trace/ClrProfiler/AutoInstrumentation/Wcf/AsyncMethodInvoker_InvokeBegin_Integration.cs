// <copyright file="AsyncMethodInvoker_InvokeBegin_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    /// <summary>
    /// System.ServiceModel.Dispatcher.AsyncMethodInvoker calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.ServiceModel",
        TypeName = "System.ServiceModel.Dispatcher.AsyncMethodInvoker",
        MethodName = "InvokeBegin",
        ReturnTypeName = ClrNames.IAsyncResult,
        ParameterTypeNames = new[] { ClrNames.Object, "System.Object[]", ClrNames.AsyncCallback, ClrNames.Object },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = WcfCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AsyncMethodInvoker_InvokeBegin_Integration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="instanceArg">RequestContext instance</param>
        /// <param name="inputs">Input arguments</param>
        /// <param name="callback">Callback argument</param>
        /// <param name="state">State argument</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object instanceArg, object[] inputs, AsyncCallback callback, object state)
        {
            // TODO Just use the OperationContext.Current object to get the span information
            // context.IncomingMessageHeaders contains:
            //  - Action
            //  - To
            //
            // context.IncomingMessageProperties contains:
            // - ["httpRequest"] key to find distributed tracing headers
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(WcfCommon.IntegrationId) || !Tracer.Instance.Settings.DelayWcfInstrumentationEnabled || WcfCommon.GetCurrentOperationContext is null)
            {
                return CallTargetState.GetDefault();
            }

            var operationContext = WcfCommon.GetCurrentOperationContext();

            if (operationContext != null && operationContext.TryDuckCast<IOperationContextStruct>(out var operationContextProxy))
            {
                var requestContext = operationContextProxy.RequestContext;

                // First, capture the active scope
                var activeScope = Tracer.Instance.InternalActiveScope;
                var spanContextRaw = DistributedTracer.Instance.GetSpanContextRaw() ?? activeScope?.Span?.Context;

                // Then, create the new scope
                var scope = WcfCommon.CreateScope(requestContext);

                if (scope != null)
                {
                    // Save the scope to retrieve it during the AsyncMethodInvoker.InvokeEnd method
                    WcfCommon.Scopes.Add(((IDuckType)requestContext).Instance, scope);
                }

                return new CallTargetState(scope, activeScope, spanContextRaw);
            }
            else
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (exception != null)
            {
                state.Scope.DisposeWithException(exception);
            }
            else if (state.Scope != null)
            {
                // InvokeBegin should have started an async operation that will ultimately call InvokeEnd
                // The current thread will be reused by WCF to process other requests so we need to restore the old scope
                if (Tracer.Instance.ScopeManager is IScopeRawAccess rawAccess)
                {
                    rawAccess.Active = state.PreviousScope;
                    DistributedTracer.Instance.SetSpanContext(state.PreviousDistributedSpanContext);
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
