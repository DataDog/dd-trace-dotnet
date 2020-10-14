using System;
using System.Threading.Tasks;
using Datadog.Trace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreWithSerilog
{
    /// <summary>
    /// This middleware invokes the rest of the ASP.NET Core pipeline in a try/catch
    /// block so any observed exception can be stored in the current Datadog span.
    /// 
    /// This middleware should be added as early in the pipeline as possible (but after the
    /// developer exception page or any error handlers) so that this middleware can observe
    /// unhandled exceptions coming from as much of the ASP.NET Core pipeline as possible.
    /// </summary>
    public class DatadogTracingExceptionLoggerMiddleware
    {
        private readonly RequestDelegate _next;

        public DatadogTracingExceptionLoggerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var currentSpan = Tracer.Instance?.ActiveScope?.Span;
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
                // The exception is never caught because `SpanSetExceptionFilter()` returns false.
                // This ensures that the developer exception page is still shown.
                when (SpanSetExceptionFilter(currentSpan, ex))
            {
            }
        }

        private bool SpanSetExceptionFilter(Span span, Exception ex)
        {
            span?.SetException(ex);
            return false;
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class DatadogTracingExceptionLoggerMiddlewareExtensions
    {
        public static IApplicationBuilder UseDatadogTracingExceptionLoggerMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<DatadogTracingExceptionLoggerMiddleware>();
        }
    }
}
