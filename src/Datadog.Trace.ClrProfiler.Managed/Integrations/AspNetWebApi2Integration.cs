using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// ApsNetWeb5Integration wraps the Web API.
    /// </summary>
    public static class AspNetWebApi2Integration
    {
        private const string OperationName = "aspnet-web-api.request";

        /// <summary>
        /// ExecuteAsync calls the underlying ExecuteAsync and traces the request.
        /// </summary>
        /// <param name="this">The Api Controller</param>
        /// <param name="controllerContext">The controller context for the call</param>
        /// <param name="cancellationTokenSource">The cancellation token source</param>
        /// <returns>A task with the result</returns>
        public static dynamic ExecuteAsync(dynamic @this, dynamic controllerContext, dynamic cancellationTokenSource)
        {
            using (Scope scope = CreateScope(controllerContext))
            {
                return scope.Span.Trace(
                    () => @this.ExecuteAsync(controllerContext, ((CancellationTokenSource)cancellationTokenSource).Token),
                    onComplete: e =>
                    {
                        if (e != null)
                        {
                            scope.Span.SetException(e);
                        }

                        // some fields aren't set till after execution, so repopulate anything missing
                        UpdateSpan(controllerContext, scope.Span);
                        scope.Span.Finish();
                    });
            }
        }

        private static Scope CreateScope(dynamic controllerContext)
        {
            var scope = Tracer.Instance.StartActive(OperationName, finishOnClose: false);
            UpdateSpan(controllerContext, scope.Span);
            return scope;
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
