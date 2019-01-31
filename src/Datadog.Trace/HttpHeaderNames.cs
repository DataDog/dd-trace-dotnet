namespace Datadog.Trace
{
    internal static class HttpHeaderNames
    {
        public const string TraceId = "x-datadog-trace-id";
        public const string ParentId = "x-datadog-parent-id";
        public const string SamplingPriority = "x-datadog-sampling-priority";
    }
}
