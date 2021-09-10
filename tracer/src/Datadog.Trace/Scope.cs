// <copyright file="Scope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// A scope is a handle used to manage the concept of an active span.
    /// Meaning that at a given time at most one span is considered active and
    /// all newly created spans that are not created with the ignoreActiveSpan
    /// parameter will be automatically children of the active span.
    /// </summary>
    public class Scope : IDisposable
    {
        private readonly IScopeManager _scopeManager;
        private readonly bool _finishOnClose;

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
        public Span Span { get; }

        internal Scope Parent { get; }

        /// <summary>
        /// Closes the current scope and makes its parent scope active
        /// </summary>
        public void Close()
        {
            _scopeManager.Close(this);

            if (_finishOnClose)
            {
                Span.Finish();
            }
        }

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
    }
}
