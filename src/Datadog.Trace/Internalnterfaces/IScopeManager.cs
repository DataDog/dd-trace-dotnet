namespace Datadog.Trace
{
    /// <summary>
    /// Provides access to activating a span and retrieving the currently active scope.
    /// </summary>
    public interface IScopeManager
    {
        /// <summary>
        /// Gets the currently active scope.
        /// </summary>
        Scope Active { get; }

        /// <summary>
        /// Activates the specified span.
        /// </summary>
        /// <param name="span">The span to activate.</param>
        /// <param name="finishOnClose">if set to <c>true</c>, finish the span when the returned scope is closed.</param>
        /// <returns>A new scope that wraps the specified span.</returns>
        Scope Activate(Span span, bool finishOnClose = true);

        /// <summary>
        /// Closes the specified scope.
        /// </summary>
        /// <param name="scope">The scope to close.</param>
        void Close(Scope scope);
    }
}