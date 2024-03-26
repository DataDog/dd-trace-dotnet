// <copyright file="SyncMethodInvokerIntegration.cs" company="Datadog">
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
    /// System.ServiceModel.Dispatcher.SyncMethodInvoker calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.ServiceModel",
        TypeName = "System.ServiceModel.Dispatcher.SyncMethodInvoker",
        MethodName = "Invoke",
        ReturnTypeName = ClrNames.Object,
        ParameterTypeNames = new[] { ClrNames.Object, "System.Object[]", "System.Object[]&" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = WcfCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SyncMethodInvokerIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="instanceArg">RequestContext instance</param>
        /// <param name="inputs">Input arguments</param>
        /// <param name="outputs">Output arguments</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object instanceArg, object[] inputs, ref object[] outputs)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(WcfCommon.IntegrationId) || !Tracer.Instance.Settings.DelayWcfInstrumentationEnabled || WcfCommon.GetCurrentOperationContext is null)
            {
                return CallTargetState.GetDefault();
            }

            return new CallTargetState(Tracer.Instance.ActiveScope as Scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (state.Scope is not null && exception is not null)
            {
                // Add the exception but do not dispose the scope.
                // BeforeSendReplyIntegration is responsible for closing the span.
                state.Scope.Span?.SetException(exception);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
