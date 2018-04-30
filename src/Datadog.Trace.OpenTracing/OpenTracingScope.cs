using System;
using OpenTracing;

namespace Datadog.Trace
{
    /// <summary>
    /// A Datadog implementation of <see cref="IScope"/>
    /// that wraps <see cref="DatadogScope"/>.
    /// </summary>
    public class OpenTracingScope : IScope
    {
        public Scope DatadogScope { get; }

        /// <summary>
        /// Gets the Span that has been scoped by this Scope.
        /// </summary>
        public OpenTracingSpan Span { get; }

        /// <summary>
        /// Gets the Span that has been scoped by this Scope.
        /// </summary>
        ISpan IScope.Span => Span;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTracingScope"/> class
        /// that wraps the specified <paramref name="datadogScope"/>.
        /// </summary>
        /// <param name="datadogScope">The Datadog <see cref="DatadogScope"/> to wrap.</param>
        public OpenTracingScope(Scope datadogScope)
        {
            DatadogScope = datadogScope;
            Span = new OpenTracingSpan(DatadogScope.Span);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            DatadogScope?.Dispose();
        }
    }
}