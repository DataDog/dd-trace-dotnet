using System;

namespace Datadog.Trace
{
    /// <summary>
    /// EventArgs for a Span
    /// </summary>
    internal readonly struct SpanEventArgs
    {
        public readonly Span _span;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanEventArgs"/> struct.
        /// Creates a new <see cref="SpanEventArgs"/> using <paramref name="span"/>
        /// </summary>
        /// <param name="span">The <see cref="Span"/> used to initialize the <see cref="SpanEventArgs"/> object.</param>
        public SpanEventArgs(Span span) => _span = span;

        internal Span Span => _span;
    }
}
