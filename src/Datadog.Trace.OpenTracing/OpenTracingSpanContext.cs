using System.Collections.Generic;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingSpanContext : SpanContext, ISpanContext
    {
        private static ILog _log = LogProvider.For<OpenTracingSpanContext>();

        internal OpenTracingSpanContext(IDatadogTracer tracer, SpanContext parent, string serviceName)
            : base(tracer, parent, serviceName)
        {
        }

        internal OpenTracingSpanContext(ulong traceId, ulong spanId)
            : base(traceId, spanId)
        {
        }

        internal OpenTracingSpanContext(SpanContext spanContext)
            : base(spanContext)
        {
        }

        public override bool Equals(object obj)
        {
            var spanContext = obj as OpenTracingSpanContext;
            if (spanContext == null)
            {
                return false;
            }

            return this.ParentId == spanContext.ParentId && this.SpanId == spanContext.SpanId && this.ServiceName == spanContext.ServiceName;
        }

        public override int GetHashCode()
        {
            return this.ParentId.GetHashCode() ^ this.SpanId.GetHashCode() ^ this.ServiceName.GetHashCode();
        }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            _log.Debug("SpanContext.GetBaggageItems is not implemented by Datadog.Trace");
            yield break;
        }
    }
}
