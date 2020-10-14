using System;
using System.Threading.Tasks;
using Datadog.Trace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreWithSerilog
{
    /// <summary>
    /// This middleware must precede all other middleware, including the developer exception page
    /// in the Development environment or any error handlers in other environments. The reason
    /// for this is that, on errors, the ASP.NET Core pipeline may be invoked again and we want
    /// the root span to capture information of the final response, including the status code.
    /// </summary>
    public class DatadogTracingRequestStartMiddleware
    {
        private readonly RequestDelegate _next;

        public DatadogTracingRequestStartMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var scope = CreateDatadogScope(context);
            try
            {
                await _next(context);
            }
            catch (Exception ex)
                // An exception should never occur here as this will be run before
                // the developer exception page or any error handlers. However,
                // if there is an exception, run the exception filter.
                //
                // The exception is never caught because `SpanSetExceptionFilter()` returns false.
                // This ensures that the developer exception page is still shown.
                when (SpanSetExceptionFilter(scope.Span, ex))
            {
            }
            finally
            {
                // Set the status code and set the aspnetcore span as an error if the status code is a 500
                var statusCode = context.Response.StatusCode;
                scope.Span.SetTag(Tags.HttpStatusCode, statusCode.ToString());
                if (statusCode / 100 == 5)
                {
                    // 5xx codes are server-side errors
                    scope.Span.Error = true;
                }

                scope.Dispose();
            }
        }

        private bool SpanSetExceptionFilter(Span span, Exception ex)
        {
            span.SetException(ex);
            return false;
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

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class DatadogTracingRequestStartMiddlewareExtensions
    {
        public static IApplicationBuilder UseDatadogTracingRequestStartMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<DatadogTracingRequestStartMiddleware>();
        }
    }
}
