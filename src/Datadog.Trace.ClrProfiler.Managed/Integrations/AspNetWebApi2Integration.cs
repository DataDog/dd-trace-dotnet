#if !NETSTANDARD2_0

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// AspNetWeb5Integration wraps the Web API.
    /// </summary>
    public static class AspNetWebApi2Integration
    {
        internal const string OperationName = "aspnet-web-api.request";

        /// <summary>
        /// Calls the underlying ExecuteAsync and traces the request.
        /// </summary>
        /// <param name="apiController">The Api Controller</param>
        /// <param name="controllerContext">The controller context for the call</param>
        /// <param name="cancellationTokenSource">The cancellation token source</param>
        /// <returns>A task with the result</returns>
        [InterceptMethod(
            TargetAssembly = "System.Web.Http",
            TargetType = "System.Web.Http.Controllers.IHttpController")]
        public static object ExecuteAsync(object apiController, object controllerContext, object cancellationTokenSource)
        {
            if (apiController == null) { throw new ArgumentNullException(nameof(apiController)); }

            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            return ExecuteAsyncInternal(apiController, controllerContext, cancellationToken);
        }

        /// <summary>
        /// Calls the underlying ExecuteAsync and traces the request.
        /// </summary>
        /// <param name="apiController">The Api Controller</param>
        /// <param name="controllerContext">The controller context for the call</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A task with the result</returns>
        private static async Task<HttpResponseMessage> ExecuteAsyncInternal(object apiController, object controllerContext, CancellationToken cancellationToken)
        {
            var controllerType = apiController.GetType();

            // in some cases, ExecuteAsync() is an explicit interface imple7mentation,
            // which is not public and has a different name, so try both
            var executeAsyncFunc =
                DynamicMethodBuilder<Func<object, object, CancellationToken, Task<HttpResponseMessage>>>
                   .GetOrCreateMethodCallDelegate(controllerType, "ExecuteAsync") ??
                DynamicMethodBuilder<Func<object, object, CancellationToken, Task<HttpResponseMessage>>>
                   .GetOrCreateMethodCallDelegate(controllerType, "System.Web.Http.Controllers.IHttpController.ExecuteAsync");

            using (var scope = Tracer.Instance.StartActive(OperationName))
            {
                try
                {
                    UpdateSpan(controllerContext, scope.Span);

                    var responseMessage = await executeAsyncFunc(apiController, controllerContext, cancellationToken).ConfigureAwait(false);

                    // some fields aren't set till after execution, so populate anything missing
                    UpdateSpan(controllerContext, scope.Span);

                    return responseMessage;
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static void UpdateSpan(dynamic controllerContext, Span span)
        {
            var req = controllerContext?.Request;

            string host = req?.Headers?.Host ?? string.Empty;
            string rawUrl = req?.RequestUri?.ToString()?.ToLowerInvariant() ?? string.Empty;
            string method = controllerContext?.Request?.Method?.Method?.ToUpperInvariant() ?? "GET";
            string route = null;
            try
            {
                route = controllerContext?.RouteData?.Route?.RouteTemplate;
            }
            catch
            {
            }

            string resourceName = $"{method} {rawUrl}";
            if (route != null)
            {
                resourceName = $"{method} {route}";
            }

            string controller = string.Empty;
            string action = string.Empty;
            try
            {
                if (controllerContext?.RouteData?.Values is IDictionary<string, object> routeValues)
                {
                    controller = (routeValues.GetValueOrDefault("controller") as string)?.ToLowerInvariant();
                    action = (routeValues.GetValueOrDefault("action") as string)?.ToLowerInvariant();
                }
            }
            catch
            {
            }

            span.ResourceName = resourceName;
            span.Type = SpanTypes.Web;
            span.SetTag(Tags.AspNetAction, action);
            span.SetTag(Tags.AspNetController, controller);
            span.SetTag(Tags.AspNetRoute, route);
            span.SetTag(Tags.HttpMethod, method);
            span.SetTag(Tags.HttpRequestHeadersHost, host);
            span.SetTag(Tags.HttpUrl, rawUrl);
        }
    }
}

#endif
