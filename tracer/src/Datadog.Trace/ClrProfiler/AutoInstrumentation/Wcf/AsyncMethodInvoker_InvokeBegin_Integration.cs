// <copyright file="AsyncMethodInvoker_InvokeBegin_Integration.cs" company="Datadog">
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
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (exception is not null)
            {
                var operationContext = WcfCommon.GetCurrentOperationContext?.Invoke();

                if (operationContext != null && operationContext.TryDuckCast<IOperationContextStruct>(out var operationContextProxy))
                {
                    var requestContext = operationContextProxy.RequestContext;

                    // Retrieve the scope that we saved during InvokeBegin
                    if (((IDuckType?)requestContext)?.Instance is object requestContextInstance
                        && WcfCommon.Scopes.TryGetValue(requestContextInstance, out var scope))
                    {
                        // Add the exception but do not dispose the span.
                        // BeforeSendReplyIntegration is responsible for closing the span.
                        scope.Span?.SetException(exception);
                    }
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
