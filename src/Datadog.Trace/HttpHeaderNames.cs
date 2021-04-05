namespace Datadog.Trace
{
    /// <summary>
    /// Names of HTTP headers that can be used tracing inbound or outbound HTTP requests.
    /// </summary>
    public static class HttpHeaderNames
    {
        /// <summary>
        /// ID of a distributed trace.
        /// </summary>
        public const string TraceId = "x-datadog-trace-id";

        /// <summary>
        /// ID of the parent span in a distributed trace.
        /// </summary>
        public const string ParentId = "x-datadog-parent-id";

        /// <summary>
        /// Setting used to determine whether a trace should be sampled or not.
        /// </summary>
        public const string SamplingPriority = "x-datadog-sampling-priority";

        /// <summary>
        /// If header is set to "false", tracing is disabled for that http request.
        /// Tracing is enabled by default.
        /// </summary>
        public const string TracingEnabled = "x-datadog-tracing-enabled";

        /// <summary>
        /// Origin of the distributed trace.
        /// </summary>
        public const string Origin = "x-datadog-origin";

        /// <summary>
        /// TraceId used for B3 propagation. Per specification, the TraceId is 64 or 128-bit in length and indicates the overall ID of the trace.
        /// </summary>
        public const string B3TraceId = "x-b3-traceid";

        /// <summary>
        /// The parent span id for B3 propagation. Per specification, the ParentSpanId is 64-bit in length and indicates the position of the parent operation in the trace tree
        /// </summary>
        public const string B3ParentId = "x-b3-parentspanid";

        /// <summary>
        /// The span id for B3 propagation. Per specification, The SpanId is 64-bit in length and indicates the position of the current operation in the trace tree.
        /// </summary>
        public const string B3SpanId = "x-b3-spanid";

        /// <summary>
        /// The sampling state for B3 propagation. 0 for a negative sampling decision, 1 for a positive sampling decision. Empty for no decision.
        /// </summary>
        public const string B3Sampled = "x-b3-sampled";

        /// <summary>
        /// The flags for B3 propagation. Currently, this is only used for indicating a debug trace when the first bit is set.
        /// </summary>
        public const string B3Flags = "x-b3-flags";
    }
}
