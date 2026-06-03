// <copyright file="AsyncControllerActionInvoker_BeginInvokeActionMethod_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Mvc.Async.AsyncControllerActionInvoker.BeginInvokeActionMethod calltarget instrumentation.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = "System.Web.Mvc.Async.AsyncControllerActionInvoker",
        MethodName = "BeginInvokeActionMethod",
        ReturnTypeName = ClrNames.IAsyncResult,
        ParameterTypeNames = new[] { ControllerContextTypeName, ActionDescriptorTypeName, DictionaryTypeName, ClrNames.AsyncCallback, ClrNames.Object },
        MinimumVersion = "4",
        MaximumVersion = "5",
        IntegrationName = IntegrationName,
        InstrumentationCategory = InstrumentationCategory.Tracing)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class AsyncControllerActionInvoker_BeginInvokeActionMethod_Integration
    {
        private const string AssemblyName = "System.Web.Mvc";
        private const string ControllerContextTypeName = "System.Web.Mvc.ControllerContext";
        private const string ActionDescriptorTypeName = "System.Web.Mvc.ActionDescriptor";
        private const string DictionaryTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.Object]";

        private const string IntegrationName = nameof(IntegrationId.AspNetMvc);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncControllerActionInvoker_BeginInvokeActionMethod_Integration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Controller context</typeparam>
        /// <typeparam name="TActionDescriptor">Action descriptor</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="controllerContext">The controller context instance.</param>
        /// <param name="actionDescriptor">The selected action descriptor instance.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="callback">Async callback.</param>
        /// <param name="state">The async state.</param>
        /// <returns>CallTarget state value.</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext, TActionDescriptor>(TTarget instance, TContext controllerContext, TActionDescriptor actionDescriptor, IDictionary<string, object> parameters, AsyncCallback callback, object state)
        {
            try
            {
                var codeOrigin = DebuggerManager.Instance.CodeOrigin;
                if (codeOrigin is { Settings.CodeOriginForSpansEnabled: true })
                {
                    var httpContext = HttpContext.Current;
                    if (SharedItems.TryPeekScope(httpContext, AspNetMvcIntegration.HttpContextKey) is { Root.Span: { } rootSpan } &&
                        !codeOrigin.HasCodeOrigin(rootSpan))
                    {
                        if (AspNetFrameworkEndpointCodeOrigin.TryGetTypeAndMethod(actionDescriptor, out var type, out var method))
                        {
                            codeOrigin.SetCodeOriginForEntrySpan(rootSpan, type, method);
                        }
                        else
                        {
                            Log.Debug("Could not extract type and method from ActionDescriptor type {ActionDescriptorType}", actionDescriptor?.GetType());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding code origin for spans in {MethodName}", "System.Web.Mvc.Async.AsyncControllerActionInvoker.BeginInvokeActionMethod()");
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
