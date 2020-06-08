namespace Datadog.Trace
{
    /// <summary>
    /// Span context interface.
    /// </summary>
    public interface ISpanContext
    {
        /// <summary>
        /// Gets the trace identifier.
        /// </summary>
        ulong TraceId { get; }

        /// <summary>
        /// Gets the span identifier.
        /// </summary>
        ulong SpanId { get; }

        /// <summary>
        /// Gets the service name to propagate to child spans.
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Gets the parent context of this context. Returns null if no parent.
        /// </summary>
        ISpanContext Parent { get; }
    }
}
