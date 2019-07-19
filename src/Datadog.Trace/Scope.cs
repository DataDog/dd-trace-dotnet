using System;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace
{
    /// <summary>
    /// A scope is a handle used to manage the concept of an active span.
    /// Meaning that at a given time at most one span is considered active and
    /// all newly created spans that are not created with the ignoreActiveSpan
    /// parameter will be automatically children of the active span.
    /// </summary>
    public class Scope : IScope
    {
        private readonly bool _finishOnClose;

        private IDisposable _stackPopJob;

        internal Scope(Scope parent, Span span, bool finishOnClose)
        {
            Parent = parent;
            Span = span;
            _finishOnClose = finishOnClose;
            _stackPopJob = DatadogScopeStack.Push(this);
        }

        /// <summary>
        /// Gets the active span wrapped in this scope
        /// </summary>
        public Span Span { get; }

        /// <summary>
        /// Gets the active span wrapped in this scope
        /// Proxy to Span without concrete return value
        /// </summary>
        ISpan IScope.Span => Span;

        internal Scope Parent { get; }

        /// <summary>
        /// Closes the current scope and makes its parent scope active
        /// </summary>
        public void Close()
        {
            if (_finishOnClose)
            {
                Span.Finish();
            }

            PopFromContextStack();
        }

        /// <summary>
        /// Closes the current scope and makes its parent scope active
        /// </summary>
        public void Dispose()
        {
            try
            {
                Close();
                PopFromContextStack();
            }
            catch
            {
                // Ignore disposal exceptions here...
                // TODO: Log? only in test/debug? How should Close() concerns be handled (i.e. independent?)
            }
        }

        private void PopFromContextStack()
        {
            _stackPopJob?.Dispose();
            _stackPopJob = null;
        }
    }
}
