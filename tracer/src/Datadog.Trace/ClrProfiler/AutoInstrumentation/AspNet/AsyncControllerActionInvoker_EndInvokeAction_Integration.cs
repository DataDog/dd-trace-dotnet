// <copyright file="AsyncControllerActionInvoker_EndInvokeAction_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.ComponentModel;
using System.Web;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Mvc.Async.AsyncControllerActionInvoker.EndInvokeAction calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = AssemblyName,
        TypeName = "System.Web.Mvc.Async.AsyncControllerActionInvoker",
        MethodName = "EndInvokeAction",
        ReturnTypeName = ClrNames.Bool,
        ParameterTypeNames = new[] { ClrNames.IAsyncResult },
        MinimumVersion = MinimumVersion,
        MaximumVersion = MaximumVersion,
        IntegrationName = IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AsyncControllerActionInvoker_EndInvokeAction_Integration
    {
        private const string AssemblyName = "System.Web.Mvc";
        private const string MinimumVersion = "4";
        private const string MaximumVersion = "5";

        private const string IntegrationName = nameof(IntegrationId.AspNetMvc);

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
        public static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult returnValue, Exception exception, CallTargetState state)
        {
            Scope scope = null;
            var httpContext = HttpContext.Current;

            try
            {
                scope = httpContext?.Items[AspNetMvcIntegration.HttpContextKey] as Scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error instrumenting method {MethodName}", "System.Web.Mvc.Async.AsyncControllerActionInvoker.EndInvokeAction()");
            }

            if (scope != null)
            {
                if (exception != null)
                {
                    scope.Span.SetException(exception);

                    // In case of exception, the status code is set further down the ASP.NET pipeline
                    // We use the OnRequestCompleted callback to be notified when that happens.
                    // We don't know how long it'll take for ASP.NET to invoke the callback,
                    // so we store the real finish time.
                    DateTimeOffset now;
                    if (scope.Span is Span span)
                    {
                        now = span.InternalContext.TraceContext.UtcNow;
                    }
                    else
                    {
                        now = DateTimeOffset.UtcNow;
                    }

                    httpContext.AddOnRequestCompleted(h => OnRequestCompleted(h, scope, now));
                }
                else
                {
                    HttpContextHelper.AddHeaderTagsFromHttpResponse(httpContext, scope);
                    scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, Tracer.InternalInstance.Settings);
                    scope.Dispose();
                }
            }

            return new CallTargetReturn<TResult>(returnValue);
        }

        private static void OnRequestCompleted(HttpContext httpContext, Scope scope, DateTimeOffset finishTime)
        {
            HttpContextHelper.AddHeaderTagsFromHttpResponse(httpContext, scope);
            scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, Tracer.InternalInstance.Settings);
            scope.Span.Finish(finishTime);
            scope.Dispose();
        }
    }
}
#endif
