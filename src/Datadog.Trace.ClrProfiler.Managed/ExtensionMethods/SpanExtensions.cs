using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.ExtensionMethods
{
    internal static class SpanExtensions
    {
        internal static string GetHttpMethod(this ISpan span)
            => span.GetTag(Tags.HttpMethod);
    }
}
