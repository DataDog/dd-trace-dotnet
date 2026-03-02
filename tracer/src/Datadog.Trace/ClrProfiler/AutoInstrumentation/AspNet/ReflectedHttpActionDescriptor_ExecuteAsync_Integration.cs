// <copyright file="ReflectedHttpActionDescriptor_ExecuteAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
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
    /// System.Web.Http.Controllers.ReflectedHttpActionDescriptor calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = SystemWebHttpAssemblyName,
        TypeName = "System.Web.Http.Controllers.ReflectedHttpActionDescriptor",
        MethodName = "ExecuteAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParameterTypeNames = new[] { HttpControllerContextTypeName, DictionaryTypeName, ClrNames.CancellationToken },
        MinimumVersion = "5.1",
        MaximumVersion = "5",
        IntegrationName = IntegrationName,
        InstrumentationCategory = InstrumentationCategory.Tracing | InstrumentationCategory.AppSec | InstrumentationCategory.Iast)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ReflectedHttpActionDescriptor_ExecuteAsync_Integration
    {
        private const string SystemWebHttpAssemblyName = "System.Web.Http";
        private const string HttpControllerContextTypeName = "System.Web.Http.Controllers.HttpControllerContext";
        private const string DictionaryTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.Object]";

        private const string IntegrationName = nameof(IntegrationId.AspNetWebApi2);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ReflectedHttpActionDescriptor_ExecuteAsync_Integration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TController">Type of the controller context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="controllerContext">The context of the controller</param>
        /// <param name="parameters">The parameters of the mvc method</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TController>(TTarget instance, TController controllerContext, IDictionary<string, object> parameters, CancellationToken cancellationToken)
            where TController : IControllerContext
        {
            try
            {
                Log.Debug("Starting {MethodName}", "System.Web.Http.Controllers.ReflectedHttpActionDescriptor.ExecuteAsync()");
                controllerContext.MonitorBodyAndPathParams(parameters, AspNetWebApi2Integration.HttpContextKey);
            }
            catch (Exception ex) when (BlockException.GetBlockException(ex) is null)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "System.Web.Http.Controllers.ReflectedHttpActionDescriptor.ExecuteAsync()");
            }

            try
            {
                var codeOrigin = DebuggerManager.Instance.CodeOrigin;
                if (codeOrigin is { Settings.CodeOriginForSpansEnabled: true })
                {
                    AddSpanCodeOrigin(instance, codeOrigin);
                }
            }
            catch (Exception ex) when (BlockException.GetBlockException(ex) is null)
            {
                Log.Error(ex, "Error adding code origin for spans in {MethodName}", "System.Web.Http.Controllers.ReflectedHttpActionDescriptor.ExecuteAsync()");
            }

            return CallTargetState.GetDefault();
        }

        private static void AddSpanCodeOrigin<TTarget>(TTarget instance, SpanCodeOrigin codeOrigin)
        {
            if (instance == null)
            {
                return;
            }

            var httpContext = HttpContext.Current;
            if (SharedItems.TryPeekScope(httpContext, AspNetWebApi2Integration.HttpContextKey) is not { Root.Span: { } rootSpan })
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "Code origin is enabled but scope was not found in HttpContext (key: {HttpContextKey}, httpContextNull: {HttpContextIsNull}, itemsCount: {HttpContextItemsCount}, actionDescriptorType: {ActionDescriptorType}).",
                        AspNetWebApi2Integration.HttpContextKey,
                        httpContext is null,
                        httpContext?.Items?.Count ?? 0,
                        instance.GetType());
                }

                return;
            }

            if (!instance.TryDuckCast<ActionDescriptorWithMethodInfo>(out var reflected)
             || reflected.MethodInfo is not { } actionMethod
             || actionMethod.DeclaringType is not { } actionType)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "Code origin is enabled but could not extract action from HttpActionDescriptor type {ActionDescriptorType} or action has no DeclaringType.",
                        instance.GetType());
                }

                return;
            }

            codeOrigin.SetCodeOriginForEntrySpan(rootSpan, actionType, actionMethod);
        }

        internal static TResponse? OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse? response, Exception? exception, in CallTargetState state)
        {
            var security = Security.Instance;
            // response can be null if action returns null
            if (security.AppsecEnabled && response is not null)
            {
                if (response.TryDuckCast<IJsonResultWebApi>(out var actionResult))
                {
                    var responseObject = actionResult.Content;
                    if (responseObject is not null)
                    {
                        var scope = SharedItems.TryPeekScope(HttpContext.Current, AspNetWebApi2Integration.HttpContextKey);
                        if (scope is not null)
                        {
                            var securityTransport = SecurityCoordinator.Get(security, scope.Span, HttpContext.Current);
                            if (!securityTransport.IsBlocked)
                            {
                                var extractedObj = ObjectExtractor.Extract(responseObject);
                                if (extractedObj is not null)
                                {
                                    var inputData = new Dictionary<string, object> { { AddressesConstants.ResponseBody, extractedObj } };
                                    securityTransport.BlockAndReport(inputData);
                                }
                            }
                        }
                        else
                        {
                            Log.Debug("Scope was null in ReflectedHttpActionDescriptor_ExecuteAsync_Integration.OnAsyncMethodEnd, cannot check security");
                        }
                    }
                }
            }

            return response;
        }
    }
}
#endif
