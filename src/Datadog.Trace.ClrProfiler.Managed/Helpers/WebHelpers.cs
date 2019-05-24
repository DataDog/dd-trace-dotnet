namespace Datadog.Trace.ClrProfiler
{
    internal static class WebHelpers
    {
        public static void DecorateWebSpan(
            this Span span,
            string resourceName,
            string method,
            string host,
            string httpUrl)
        {
            span.Type = SpanTypes.Web;
            span.ResourceName = resourceName;
            span.SetTag(Tags.HttpMethod, method);
            span.SetTag(Tags.HttpRequestHeadersHost, host);
            span.SetTag(Tags.HttpUrl, httpUrl);
        }
    }
}
