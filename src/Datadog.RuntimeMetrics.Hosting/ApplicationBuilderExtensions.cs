using System;
using System.Globalization;
using Datadog.Trace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Datadog.RuntimeMetrics.Hosting
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds Datadog tracing middleware.
        /// </summary>
        public static IApplicationBuilder UseDatadogTracing(this IApplicationBuilder app, Tracer tracer, int maxSpans, int maxTags)
        {
            if(maxSpans == 0)
            {
                // noop
                return app;
            }

            var random = new Random();

            app.Use(async (context, next) =>
                    {
                        HttpRequest request = context.Request;
                        string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
                        string url = GetUrl(request);
                        string resourceUrl = new Uri(url).AbsolutePath.ToLowerInvariant();
                        string resourceName = $"{httpMethod} {resourceUrl}";

                        // always create at least 1 span
                        using Scope middlewareScope = tracer.StartActive("middleware");
                        Span middlewareSpan = middlewareScope.Span;
                        middlewareSpan.Type = SpanTypes.Web;
                        middlewareSpan.ResourceName = resourceName.Trim();
                        middlewareSpan.SetTag(Tags.SpanKind, SpanKinds.Server);
                        middlewareSpan.SetTag(Tags.HttpMethod, httpMethod);
                        middlewareSpan.SetTag(Tags.HttpRequestHeadersHost, request.Host.Value);
                        middlewareSpan.SetTag(Tags.HttpUrl, url);
                        middlewareSpan.SetTag(Tags.Language, "dotnet");

                        // we already created 1 span, so create up to maxSpans - 1 additional spans
                        int spanCount = random.Next(0, maxSpans - 1);

                        for (int spanIndex = 0; spanIndex < spanCount - 1; spanIndex++)
                        {
                            using Scope innerScope = tracer.StartActive("manual");
                            Span innerSpan = innerScope.Span;
                            innerSpan.Type = SpanTypes.Custom;

                            int tagCount = random.Next(0, maxTags);

                            for (int tagIndex = 0; tagIndex < tagCount; tagIndex++)
                            {
                                innerSpan.SetTag($"tag{tagIndex}", tagIndex.ToString());
                            }
                        }

                        // call the next middleware in the chain
                        await next.Invoke();

                        middlewareScope?.Span.SetTag(Tags.HttpStatusCode, context.Response.StatusCode.ToString(CultureInfo.InvariantCulture));
                    });


            return app;
        }

        private static string GetUrl(HttpRequest request)
        {
            // HTTP 1.0 requests are not required to provide a Host to be valid
            var host = request.Host.HasValue ? request.Host.Value : "UNKNOWN_HOST";
            return $"{request.Scheme}://{host}{request.PathBase.Value}{request.Path.Value}";
        }
    }
}
