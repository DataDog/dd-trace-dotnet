using System;
using OpenTracing;

namespace Datadog.Trace
{
    /// <summary>
    /// A Datadog implementation of <see cref="OpenTracing.IScope"/>
    /// that wraps <see cref="Scope"/>.
    /// </summary>
    public class OpenTracingScope : IScope
    {
        private readonly Scope _scope;

        private OpenTracingSpan _span;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTracingScope"/> class
        /// that wraps the specified <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The Datadog <see cref="Scope"/> to wrap.</param>
        public OpenTracingScope(Scope scope)
        {
            _scope = scope;
            _span = new OpenTracingSpan(_scope); 
        }

        /// <inheritdoc />
        public ISpan Span => _span;

        /// <inheritdoc />
        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}