using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.ExtensionMethods
{
    internal static class SpanExtensions
    {
        internal static string GetHttpMethod(this ISpan span)
            => span.GetTag(Tags.HttpMethod);

        internal static void DecorateWebSpan(
            this Span span,
            string resourceName,
            string method,
            string host,
            string httpUrl)
        {
            span.Type = SpanTypes.Web;
            span.ResourceName = resourceName?.Trim();
            span.SetTag(Tags.HttpMethod, method);
            span.SetTag(Tags.HttpRequestHeadersHost, host);
            span.SetTag(Tags.HttpUrl, httpUrl);
        }
    }
}
