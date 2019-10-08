using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingSpanContext : global::OpenTracing.ISpanContext
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<OpenTracingSpanContext>();

        public OpenTracingSpanContext(ISpanContext context)
        {
            Context = context;
        }

        public string TraceId => Context.TraceId.ToString(CultureInfo.InvariantCulture);

        public string SpanId => Context.SpanId.ToString(CultureInfo.InvariantCulture);

        internal ISpanContext Context { get; }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}
