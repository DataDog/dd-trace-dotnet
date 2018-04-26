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

        private OpenTracingSpan _lastActiveSpan;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTracingScope"/> class
        /// that wraps the specified <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The Datadog <see cref="Scope"/> to wrap.</param>
        public OpenTracingScope(Scope scope)
        {
            _scope = scope;
        }

        /// <inheritdoc />
        public ISpan Span
        {
            get
            {
                // as long as the active span is the same instance, keep returning the same wrapper;
                // else, create a new wrapper
                if (!ReferenceEquals(_lastActiveSpan.DDSpan, _scope.Span))
                {
                    _lastActiveSpan = new OpenTracingSpan(_scope.Span);
                }

                return _lastActiveSpan;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}