namespace Datadog.Trace.ServiceFabric
{
    internal readonly struct PropagationContext
    {
        public readonly ulong TraceId;
        public readonly ulong ParentSpanId;
        public readonly int? SamplingPriority;
        public readonly string? Origin;

        public PropagationContext(ulong traceId, ulong parentSpanId, int? samplingPriority, string? origin)
        {
            TraceId = traceId;
            ParentSpanId = parentSpanId;
            SamplingPriority = samplingPriority;
            Origin = origin;
        }
    }
}
