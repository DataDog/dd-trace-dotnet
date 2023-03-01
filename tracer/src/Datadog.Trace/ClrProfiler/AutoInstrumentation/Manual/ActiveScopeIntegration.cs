// <copyright file="ActiveScopeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Net;
using Datadog.Trace.Activity;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Manual
{
    /// <summary>
    /// CallTarget integration for WebRequest.GetResponseAsync
    /// We're actually instrumenting HttpWebRequest, but the GetResponseAsync method is declared in WebRequest (and not overriden)
    /// So instead, we instrument WebRequest and check the actual type
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Datadog.DiagnosticSource",
        TypeName = "Datadog.DiagnosticSource.Tracer",
        MethodName = "get_CurrentDatadogActivity",
        ReturnTypeName = ClrNames.Object,
        MinimumVersion = "1.*.*",
        MaximumVersion = "1.*.*",
        IntegrationName = WebRequestCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ActiveScopeIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (Tracer.Instance.InternalActiveScope is null)
            {
                return CallTargetState.GetDefault();
            }

            return new CallTargetState(Tracer.Instance.InternalActiveScope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Existing return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (state.Scope is null)
            {
                return new CallTargetReturn<TReturn>(returnValue);
            }

            return new CallTargetReturn<TReturn>((TReturn)(object)state.Scope.Span);
        }
    }
}
