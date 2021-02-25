#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.ClrProfiler.Integrations.AspNet;
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
    public class ApiController_ExecuteAsync_Integration
    {
        private const string SystemWebHttpAssemblyName = "System.Web.Http";
        private const string HttpControllerContextTypeName = "System.Web.Http.Controllers.HttpControllerContext";
        private const string Major5Minor1 = "5.1";
        private const string Major5MinorX = "5";

        private const string IntegrationName = nameof(IntegrationIds.AspNetWebApi2);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TController">Type of the controller context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="controllerContext">The context of the controller</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TController>(TTarget instance, TController controllerContext, CancellationToken cancellationToken)
            where TController : IHttpControllerContext
        {
            // Make sure to box the controllerContext proxy only once
            var boxedControllerContext = (IHttpControllerContext)controllerContext;

            var scope = AspNetWebApi2Integration.CreateScope(boxedControllerContext, out _);

            if (scope != null)
            {
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
        public static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, CallTargetState state)
        {
            var scope = state.Scope;

            if (scope is null)
            {
                return responseMessage;
            }

            var controllerContext = (IHttpControllerContext)state.State;

            // some fields aren't set till after execution, so populate anything missing
            AspNetWebApi2Integration.UpdateSpan(controllerContext, scope.Span, (AspNetTags)scope.Span.Tags, Enumerable.Empty<KeyValuePair<string, string>>());

            if (exception != null)
            {
                scope.Span.SetException(exception);

                // We don't have access to the final status code at this point
                // Ask the HttpContext to call us back to that we can get it
                var httpContext = HttpContext.Current;

                if (httpContext != null)
                {
                    // We don't know how long it'll take for ASP.NET to invoke the callback,
                    // so we store the real finish time
                    var now = scope.Span.Context.TraceContext.UtcNow;
                    httpContext.AddOnRequestCompleted(h => OnRequestCompleted(h, scope, now));
                }
                else
                {
                    // Looks like we won't be able to get the final status code
                    scope.Dispose();
                }
            }
            else
            {
                var statusCode = responseMessage.DuckCast<HttpResponseMessageStruct>().StatusCode;
                scope.Span.SetHttpStatusCode(statusCode, isServer: true);
                scope.Dispose();
            }

            return responseMessage;
        }

        private static void OnRequestCompleted(HttpContext httpContext, Scope scope, DateTimeOffset finishTime)
        {
            scope.Span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true);
            scope.Span.Finish(finishTime);
            scope.Dispose();
        }
    }
}
#endif
