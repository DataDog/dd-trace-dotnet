namespace Datadog.Trace
{
    internal static class HttpHeaderNames
    {
        public const string HttpHeaderTraceId = "x-datadog-trace-id";
        public const string HttpHeaderParentId = "x-datadog-parent-id";
        public const string HttpHeaderSamplingPriority = "x-datadog-sampling-priority";
    }
}
