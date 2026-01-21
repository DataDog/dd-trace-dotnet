// <copyright file="ControllerActionInvoker_InvokeAction_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Mvc.Async.AsyncControllerActionInvoker.BeginInvokeAction calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = "System.Web.Mvc.ControllerActionInvoker",
        MethodName = "InvokeActionMethod",
        ReturnTypeName = ActionResultTypeName,
        ParameterTypeNames = new[] { ControllerContextTypeName, ActionDescriptorTypeName, DictionaryTypeName },
        MinimumVersion = "4",
        MaximumVersion = "5",
        IntegrationName = IntegrationName,
        InstrumentationCategory = InstrumentationCategory.AppSec | InstrumentationCategory.Iast)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ControllerActionInvoker_InvokeAction_Integration
    {
        private const string AssemblyName = "System.Web.Mvc";
        private const string ActionResultTypeName = "System.Web.Mvc.ActionResult";
        private const string ControllerContextTypeName = "System.Web.Mvc.ControllerContext";
        private const string ActionDescriptorTypeName = "System.Web.Mvc.ActionDescriptor";
        private const string DictionaryTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.Object]";

        private const string IntegrationName = nameof(IntegrationId.AspNetMvc);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ControllerActionInvoker_InvokeAction_Integration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Controller context</typeparam>
        /// <typeparam name="TActionDescriptor">Action descriptor</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="controllerContext">The control context instance</param>
        /// <param name="actionDescriptor">The action descriptor instance</param>
        /// <param name="parameters">The parameters of the mvc method</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext, TActionDescriptor>(TTarget instance, TContext controllerContext, TActionDescriptor actionDescriptor, IDictionary<string, object> parameters)
            where TContext : IControllerContext
        {
            try
            {
                controllerContext.MonitorBodyAndPathParams(parameters, AspNetMvcIntegration.HttpContextKey);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "System.Web.Mvc.ControllerActionInvoker.InvokeActionMethod()");
            }

            try
            {
                var codeOrigin = DebuggerManager.Instance.CodeOrigin;
                if (codeOrigin is { Settings.CodeOriginForSpansEnabled: true })
                {
                    AddSpanCodeOrigin(actionDescriptor, codeOrigin);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding code origin for spans in {MethodName}", "System.Web.Mvc.ControllerActionInvoker.InvokeActionMethod()");
            }

            return CallTargetState.GetDefault();
        }

        private static void AddSpanCodeOrigin<TActionDescriptor>(TActionDescriptor actionDescriptor, SpanCodeOrigin codeOrigin)
        {
            if (SharedItems.TryPeekScope(HttpContext.Current, AspNetMvcIntegration.HttpContextKey) is not { Root.Span: { } rootSpan })
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Code origin is enabled for spans but MVC scope was not found in HttpContext.");
                }

                return;
            }

            if (actionDescriptor is null)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Code origin is enabled for spans but MVC ActionDescriptor instance was null.");
                }

                return;
            }

            if (!actionDescriptor.TryDuckCast<ActionDescriptorWithMethodInfo>(out var reflected) || reflected.MethodInfo is not { } actionMethod)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "Code origin is enabled for spans but could not extract MVC action MethodInfo from ActionDescriptor type {ActionDescriptorType}.",
                        actionDescriptor.GetType());
                }

                return;
            }

            if (actionMethod.DeclaringType is not { } actionType)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "Code origin is enabled for spans but extracted MVC action MethodInfo has no DeclaringType. Method: {Method}.",
                        actionMethod);
                }

                return;
            }

            codeOrigin.SetCodeOriginForEntrySpan(rootSpan, actionType, actionMethod);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResult">TestResult type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Original method return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>Return value of the method</returns>
        internal static CallTargetReturn<TResult?> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult? returnValue, Exception? exception, in CallTargetState state)
        {
            var security = Security.Instance;
            if (security.AppsecEnabled && returnValue is not null)
            {
                if (returnValue.TryDuckCast<IJsonResultMvc>(out var actionResult))
                {
                    var responseObject = actionResult.Data;
                    if (responseObject is not null)
                    {
                        var scope = SharedItems.TryPeekScope(HttpContext.Current, AspNetMvcIntegration.HttpContextKey);
                        if (scope is not null)
                        {
                            var securityTransport = SecurityCoordinator.Get(security, scope.Span, HttpContext.Current);
                            if (!securityTransport.IsBlocked)
                            {
                                var extractedObject = ObjectExtractor.Extract(responseObject);
                                if (extractedObject is not null)
                                {
                                    var inputData = new Dictionary<string, object> { { AddressesConstants.ResponseBody, extractedObject } };
                                    securityTransport.BlockAndReport(inputData);
                                }
                            }
                        }
                        else
                        {
                            Log.Debug("Scope was null in ControllerActionInvoker_InvokeAction_Integration.OnMethodEnd, cannot check security");
                        }
                    }
                }
            }

            return new CallTargetReturn<TResult?>(returnValue);
        }
    }
}
#endif
