using System;

namespace Datadog.Trace
{
    internal static class DatadogCallContext
    {
        public static AsyncLocalCompat<Guid?> UniqueId { get; } = new AsyncLocalCompat<Guid?>();

        public static AsyncLocalCompat<ulong?> TraceId { get; } = new AsyncLocalCompat<ulong?>();

        public static AsyncLocalCompat<ulong?> ParentSpanId { get; } = new AsyncLocalCompat<ulong?>();

        public static AsyncLocalCompat<ulong?> SpanId { get; } = new AsyncLocalCompat<ulong?>();
    }
}
