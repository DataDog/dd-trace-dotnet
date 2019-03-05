namespace Datadog.Trace
{
    /// <summary>
    /// Valid values for <see cref="SpanContext.SamplingPriority"/>.
    /// These values determine whether a trace should be sampled or not.
    /// </summary>
    public enum SamplingPriority
    {
        /// <summary>
        /// Explicitly ask the backend to not store a trace.
        /// </summary>
        UserReject = -1,

        /// <summary>
        /// Used by the built-in sampler to inform the backend that a trace should be rejected and not stored.
        /// </summary>
        AutoReject = 0,

        /// <summary>
        /// Used by the built-in sampler to inform the backend that a trace should be kept and stored.
        /// </summary>
        AutoKeep = 1,

        /// <summary>
        /// Explicitly ask the backend to keep a trace.
        /// </summary>
        UserKeep = 2
    }
}
