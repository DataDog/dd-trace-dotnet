// <copyright file="AsyncMethodInvoker_InvokeBegin_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;

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
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AsyncMethodInvoker_InvokeBegin_Integration
    {
        private const string IntegrationName = nameof(IntegrationIds.Wcf);
        private static readonly Func<object> _getCurrentOperationContext;

        static AsyncMethodInvoker_InvokeBegin_Integration()
        {
            var operationContextType = Type.GetType("System.ServiceModel.OperationContext, System.ServiceModel, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", throwOnError: false);
            if (operationContextType is not null)
            {
                var property = operationContextType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                var method = property.GetGetMethod();
                _getCurrentOperationContext = (Func<object>)method.CreateDelegate(typeof(Func<object>));
            }
        }

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
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object instanceArg, object[] inputs, AsyncCallback callback, object state)
        {
            // TODO Just use the OperationContext.Current object to get the span information
            // context.IncomingMessageHeaders contains:
            //  - Action
            //  - To
            //
            // context.IncomingMessageProperties contains:
            // - ["httpRequest"] key to find distributed tracing headers
            if (_getCurrentOperationContext is null || !Tracer.Instance.Settings.WcfEnableNewInstrumentation)
            {
                return CallTargetState.GetDefault();
            }

            var requestContext = _getCurrentOperationContext()?.GetProperty<object>("RequestContext").GetValueOrDefault();
            return new CallTargetState(WcfIntegration.CreateScope(requestContext));
        }
    }
}
#endif
