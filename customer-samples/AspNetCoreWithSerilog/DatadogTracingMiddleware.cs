using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace AspNetCoreWithSerilog
{
    public class DatadogTracingMiddleware
    {
        private readonly RequestDelegate _next;

        public DatadogTracingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            using (var scope = CreateDatadogScope(context))
            {
                await _next(context);
            }
        }

        private Scope CreateDatadogScope(HttpContext context)
        {
            Scope scope = Tracer.Instance.StartActive("aspnet_core.request");
            Span span = scope.Span;
            HttpRequest request = context.Request;

            var httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            var resourceName = $"{request.PathBase.Value}{request.Path.Value}";

            span.Type = SpanTypes.Web;
            span.ResourceName = resourceName;
            span.SetTag(Tags.SpanKind, SpanKinds.Server);
            span.SetTag(Tags.HttpMethod, httpMethod);
            span.SetTag(Tags.HttpRequestHeadersHost, request.Host.Value);
            span.SetTag(Tags.HttpUrl, GetUrl(request));

            return scope;
        }

        /// <summary>
        /// Helper to get the URL from an HttpRequest
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static string GetUrl(HttpRequest request)
        {
            if (request.Host.HasValue)
            {
                return $"{request.Scheme}://{request.Host.Value}{request.PathBase.Value}{request.Path.Value}";
            }

            // HTTP 1.0 requests are not required to provide a Host to be valid
            // Since this is just for display, we can provide a string that is
            // not an actual Uri with only the fields that are specified.
            // request.GetDisplayUrl(), used above, will throw an exception
            // if request.Host is null.
            return $"{request.Scheme}://UNKNOWN_HOST{request.PathBase.Value}{request.Path.Value}";
        }
    }
}
