// <copyright file="Scope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// A scope is a handle used to manage the concept of an active span.
    /// Meaning that at a given time at most one span is considered active and
    /// all newly created spans that are not created with the ignoreActiveSpan
    /// parameter will be automatically children of the active span.
    /// </summary>
    internal partial class Scope : IScope
    {
        private readonly IScopeManager _scopeManager;
        private bool _finishOnClose;

        internal Scope(Scope parent, Span span, IScopeManager scopeManager, bool finishOnClose)
        {
            Parent = parent;
            Span = span;
            _scopeManager = scopeManager;
            _finishOnClose = finishOnClose;
        }

        /// <summary>
        /// Gets the active span wrapped in this scope
        /// </summary>
        internal Span Span { get; }

        internal Scope Parent { get; }

        internal Scope Root => Parent?.Root ?? this;

        /// <summary>
        /// Closes the current scope and makes its parent scope active
        /// </summary>
        public void Dispose()
        {
            try
            {
                Close();
            }
            catch
            {
                // Ignore disposal exceptions here...
                // TODO: Log? only in test/debug? How should Close() concerns be handled (i.e. independent?)
            }
        }

        /// <summary>
        /// Closes the current scope and makes its parent scope active
        /// </summary>
        internal void Close()
        {
            _scopeManager.Close(this);

            if (_finishOnClose)
            {
                Span.Finish();
            }
        }

        internal void SetFinishOnClose(bool finishOnClose)
        {
            _finishOnClose = finishOnClose;
        }
    }
}
