namespace Datadog.Trace
{
    internal class PropagationContext : ISpanContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropagationContext"/> class.
        /// </summary>
        /// <param name="traceId">The trace id.</param>
        /// <param name="spanId">The span id.</param>
        /// <param name="samplingPriority">The sampling priority.</param>
        public PropagationContext(ulong traceId, ulong spanId, SamplingPriority? samplingPriority)
        {
            TraceId = traceId;
            SpanId = spanId;
            SamplingPriority = samplingPriority;
        }

        /// <summary>
        /// Gets the trace id.
        /// </summary>
        public ulong TraceId { get; }

        /// <summary>
        /// Gets the span id.
        /// </summary>
        public ulong SpanId { get; }

        /// <summary>
        /// Gets the sampling priority.
        /// </summary>
        public SamplingPriority? SamplingPriority { get; }
    }
}
