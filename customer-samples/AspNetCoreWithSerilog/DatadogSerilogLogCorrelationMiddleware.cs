using System;
using System.Threading.Tasks;
using Datadog.Trace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace AspNetCoreWithSerilog
{
    /// <summary>
    /// This middleware must follow the <see cref="DatadogTracingMiddleware"/> since this
    /// depends on a Datadog span already being started. This middleware is not needed if
    /// automatic logs injection is enabled by setting DD_LOGS_INJECTION=true.
    /// </summary>
    public class DatadogSerilogLogCorrelationMiddleware
    {
        private readonly RequestDelegate _next;

        public DatadogSerilogLogCorrelationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            using (LogContext.PushProperty("dd_env", CorrelationIdentifier.Env)) // Consumes DD_ENV environment variable
            using (LogContext.PushProperty("dd_service", CorrelationIdentifier.Service)) // Consumes DD_SERVICE environment variable (or application name)
            using (LogContext.PushProperty("dd_version", CorrelationIdentifier.Version)) // Consumes DD_VERSION environment variable
            using (LogContext.PushProperty("dd_trace_id", CorrelationIdentifier.TraceId.ToString()))
            using (LogContext.PushProperty("dd_span_id", CorrelationIdentifier.SpanId.ToString()))
            {
                await _next(context);
            }
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class DatadogSerilogLogCorrelationMiddlewareExtensions
    {
        public static IApplicationBuilder UseDatadogSerilogLogCorrelationMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<DatadogSerilogLogCorrelationMiddleware>();
        }
    }
}
