using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Interface for managing a scope.
    /// </summary>
    public interface IScopeManager
    {
        /// <summary>
        /// Emitted when a span is started.
        /// </summary>
        event EventHandler<SpanEventArgs> SpanOpened;

        /// <summary>
        /// Emitted when a span is made the current one.
        /// </summary>
        event EventHandler<SpanEventArgs> SpanActivated;

        /// <summary>
        /// Emitted when a span is no longer the current one.
        /// </summary>
        event EventHandler<SpanEventArgs> SpanDeactivated;

        /// <summary>
        /// Emitted when a span completes.
        /// </summary>
        event EventHandler<SpanEventArgs> SpanClosed;

        /// <summary>
        /// Emitted when a trace finishes.
        /// </summary>
        event EventHandler<SpanEventArgs> TraceEnded;

        /// <summary>
        /// Gets the current active scope for a trace.
        /// </summary>
        Scope Active { get; }

        /// <summary>
        /// Start a new active scope for a trace.
        /// </summary>
        /// <param name="span">The span to make active.</param>
        /// <param name="finishOnClose">Whether to finish the span when disposed.</param>
        /// <returns>The active scope.</returns>
        Scope Activate(Span span, bool finishOnClose);

        /// <summary>
        /// Finish a scope.
        /// </summary>
        /// <param name="scope">The scope to finish.</param>
        void Close(Scope scope);
    }
}
