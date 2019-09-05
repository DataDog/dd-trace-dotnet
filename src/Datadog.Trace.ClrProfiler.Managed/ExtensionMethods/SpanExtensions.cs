using Datadog.Trace;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.ExtensionMethods
{
    internal static class SpanExtensions
    {
        internal static string GetHttpMethod(this ISpan span)
            => span.GetTag(Tags.HttpMethod);

        internal static string GetHost(this ISpan span)
            => span.GetTag(Tags.HttpRequestHeadersHost);

        internal static string GetAbsoluteUrl(this ISpan span)
            => span.GetTag(Tags.HttpUrl);
    }
}
