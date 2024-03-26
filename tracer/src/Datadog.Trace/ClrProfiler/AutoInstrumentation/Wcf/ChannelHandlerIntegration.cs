// <copyright file="ChannelHandlerIntegration.cs" company="Datadog">
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
    /// System.ServiceModel.Dispatcher.ChannelHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.ServiceModel",
        TypeName = "System.ServiceModel.Dispatcher.ChannelHandler",
        MethodName = "HandleRequest",
        ReturnTypeName = ClrNames.Bool,
        ParameterTypeNames = new[] { "System.ServiceModel.Channels.RequestContext", "System.ServiceModel.OperationContext" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ChannelHandlerIntegration
    {
        private const string IntegrationName = nameof(Configuration.IntegrationId.Wcf);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TRequestContext">Type of the request context</typeparam>
        /// <typeparam name="TOperationContext">Type of the operation context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="request">RequestContext instance</param>
        /// <param name="currentOperationContext">OperationContext instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TRequestContext, TOperationContext>(TTarget instance, TRequestContext request, TOperationContext currentOperationContext)
            where TRequestContext : IRequestContext, IDuckType
        {
            if (Tracer.Instance.Settings.DelayWcfInstrumentationEnabled || request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            // webHttpResourceNames aren't available here, so no point in checking
            return new CallTargetState(WcfCommon.CreateScope(request.RequestMessage, useWebHttpResourceNames: false));
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
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
