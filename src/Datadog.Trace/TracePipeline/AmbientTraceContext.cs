using System;
using System.Threading;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace
{
    /// <summary>
    /// Container for necessary ambient trace context
    /// </summary>
    public class AmbientTraceContext
    {
        private static readonly ThreadLocal<Random> Random = new ThreadLocal<Random>(() => new Random());

        /// <summary>
        /// Gets the current TraceId for the current context
        /// </summary>
        public ulong TraceId { get; private set; }

        /// <summary>
        /// Gets the current ParentSpanId for the current context
        /// </summary>
        public ulong? ParentSpanId { get; private set; }

        /// <summary>
        /// Gets the current SpanId for the current context
        /// </summary>
        public ulong SpanId { get; private set; }

        /// <summary>
        /// Gets the current SpanUniqueId for the current context
        /// </summary>
        public Guid SpanUniqueId { get; private set; }

        /// <summary>
        /// Create a top level ambient context.
        /// </summary>
        /// <returns>The top level context for a trace.</returns>
        public static AmbientTraceContext CreateTopLevelContext()
        {
            var traceId = Random.Value.NextUInt63();
            return new AmbientTraceContext
            {
                TraceId = traceId,
                SpanId = traceId,
                ParentSpanId = null,
                SpanUniqueId = Guid.NewGuid()
            };
        }

        /// <summary>
        /// Creates a child of another context.
        /// </summary>
        /// <param name="parent">The parent of this new context.</param>
        /// <returns>The child context.</returns>
        public AmbientTraceContext CreateChild(AmbientTraceContext parent)
        {
            return new AmbientTraceContext
            {
                TraceId = parent.TraceId,
                ParentSpanId = parent.SpanId,
                SpanUniqueId = Guid.NewGuid(),
                SpanId = Random.Value.NextUInt63()
            };
        }
    }
}