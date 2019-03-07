using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingPropagationContext : global::OpenTracing.ISpanContext
    {
        private static ILog _log = LogProvider.For<OpenTracingPropagationContext>();

        public OpenTracingPropagationContext(PropagationContext context)
        {
            Context = context;
        }

        public string TraceId => Context.TraceId.ToString(CultureInfo.InvariantCulture);

        public string SpanId => Context.SpanId.ToString(CultureInfo.InvariantCulture);

        internal PropagationContext Context { get; }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}