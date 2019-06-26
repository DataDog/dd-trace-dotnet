using System;

namespace Datadog.Trace
{
    internal class SpanEventArgs : EventArgs
    {
        public SpanEventArgs(Span span)
        {
            Span = span;
        }

        internal Span Span { get; }
    }
}
