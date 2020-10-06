namespace Datadog.Trace.ServiceFabric
{
    internal struct PropagationContext
    {
        public ulong TraceId;
        public ulong ParentSpanId;
        public int SamplingPriority;
        public string Origin;
    }
}
