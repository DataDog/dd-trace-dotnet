// <copyright file="ApiController_ExecuteAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// System.Web.Http.ApiController.ExecuteAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = SystemWebHttpAssemblyName,
        TypeName = "System.Web.Http.ApiController",
        MethodName = "ExecuteAsync",
        ReturnTypeName = ClrNames.HttpResponseMessageTask,
        ParameterTypeNames = new[] { HttpControllerContextTypeName, ClrNames.CancellationToken },
        MinimumVersion = Major5Minor1,
        MaximumVersion = Major5MinorX,
        IntegrationName = IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ApiController_ExecuteAsync_Integration
    {
        private const string SystemWebHttpAssemblyName = "System.Web.Http";
        private const string HttpControllerContextTypeName = "System.Web.Http.Controllers.HttpControllerContext";
        private const string Major5Minor1 = "5.1";
        private const string Major5MinorX = "5";

        private const string IntegrationName = nameof(IntegrationId.AspNetWebApi2);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TController">Type of the controller context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="controllerContext">The context of the controller</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TController>(TTarget instance, TController controllerContext, CancellationToken cancellationToken)
            where TController : IHttpControllerContext
        {
            // Make sure to box the controllerContext proxy only once
            var boxedControllerContext = (IHttpControllerContext)controllerContext;

            var scope = AspNetWebApi2Integration.CreateScope(boxedControllerContext, out _);

            if (scope != null)
            {
                SharedItems.PushScope(HttpContext.Current, AspNetWebApi2Integration.HttpContextKey, scope);
                return new CallTargetState(scope, boxedControllerContext);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="responseMessage">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        [PreserveContext]
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, in CallTargetState state)
        {
            var httpContext = HttpContext.Current;

            var scope = state.Scope;
            SharedItems.TryPopScope(httpContext, AspNetWebApi2Integration.HttpContextKey);

            if (scope is null)
            {
                return responseMessage;
            }

            var controllerContext = (IHttpControllerContext)state.State;

            // some fields aren't set till after execution, so populate anything missing
            AspNetWebApi2Integration.UpdateSpan(controllerContext, scope.Span, (AspNetTags)scope.Span.Tags);

            if (exception != null)
            {
                scope.Span.SetException(exception);

                // We don't have access to the final status code at this point
                // Ask the HttpContext to call us back to that we can get it
                if (httpContext != null)
                {
                    // We don't know how long it'll take for ASP.NET to invoke the callback,
                    // so we store the real finish time
                    // Additionally, update the scope so it does not finish the span on close. This allows
                    // us to defer finishing the span later while making sure callers of this method do not
                    // get this scope when calling Tracer.ActiveScope
                    var now = scope.Span.Context.TraceContext.Clock.UtcNow;
                    httpContext.AddOnRequestCompleted(h => OnRequestCompletedAfterException(h, scope, now));

                    scope.SetFinishOnClose(false);
                    scope.Dispose();
                }
                else
                {
                    // Looks like we won't be able to get the final status code
                    scope.Dispose();
                }
            }
            else
            {
                HttpContextHelper.AddHeaderTagsFromHttpResponse(HttpContext.Current, scope);
                scope.Span.SetHttpStatusCode(responseMessage.DuckCast<HttpResponseMessageStruct>().StatusCode, isServer: true, Tracer.Instance.Settings);
                scope.Dispose();
            }

            return responseMessage;
        }

        private static void OnRequestCompletedAfterException(HttpContext httpContext, Scope scope, DateTimeOffset finishTime)
        {
            HttpContextHelper.AddHeaderTagsFromHttpResponse(httpContext, scope);
            scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, Tracer.Instance.Settings);
            scope.Span.Finish(finishTime);
        }
    }
}
#endif
