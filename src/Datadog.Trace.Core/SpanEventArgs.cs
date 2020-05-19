using System;

namespace Datadog.Trace
{
    /// <summary>
    /// EventArgs for a Span
    /// </summary>
    public class SpanEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanEventArgs"/> class.
        /// Creates a new <see cref="SpanEventArgs"/> using <paramref name="span"/>
        /// </summary>
        /// <param name="span">The <see cref="Span"/> used to initialize the <see cref="SpanEventArgs"/> object.</param>
        public SpanEventArgs(Span span) => Span = span;

        /// <summary>
        /// Gets the span to feed the event arguments
        /// </summary>
        internal Span Span { get; }
    }
}
