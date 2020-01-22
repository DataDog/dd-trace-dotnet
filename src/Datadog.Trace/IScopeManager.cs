using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Interface for managing a scope.
    /// </summary>
    internal interface IScopeManager
    {
        /// <summary>
        /// SpanOpened event delegate
        /// </summary>
        event EventHandler<SpanEventArgs> SpanOpened;

        /// <summary>
        /// SpanActivated event delegate
        /// </summary>
        event EventHandler<SpanEventArgs> SpanActivated;

        /// <summary>
        /// SpanDeactivated event delegate
        /// </summary>
        event EventHandler<SpanEventArgs> SpanDeactivated;

        /// <summary>
        /// SpanClosed event delegate
        /// </summary>
        event EventHandler<SpanEventArgs> SpanClosed;

        /// <summary>
        /// TraceEnded event delegate
        /// </summary>
        event EventHandler<SpanEventArgs> TraceEnded;

        /// <summary>
        /// Gets the active scope
        /// </summary>
        Scope Active { get; }

        /// <summary>
        /// Activates a scope based on <paramref name="span"/>.
        /// </summary>
        /// <param name="span">The span starting the scope to be activated</param>
        /// <param name="finishOnClose">Whether <paramref name="span"/> should be the last span in the activated scope.</param>
        /// <returns>A <see cref="Scope"/></returns>
        Scope Activate(Span span, bool finishOnClose);

        /// <summary>
        /// Close <paramref name="scope"/>
        /// </summary>
        /// <param name="scope">The <see cref="Scope"/> to be closed</param>
        void Close(Scope scope);
    }
}
