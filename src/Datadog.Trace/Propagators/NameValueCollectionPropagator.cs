using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Propagators
{
    internal class NameValueCollectionPropagator : IPropagator
    {
        private readonly NameValueCollection _headers;

        public NameValueCollectionPropagator(NameValueCollection headers)
        {
            _headers = headers;
        }

        public void Inject(SpanContext context)
        {
            _headers[HttpHeaderNames.TraceId] = context.TraceId.ToString(CultureInfo.InvariantCulture);
            _headers[HttpHeaderNames.ParentId] = context.SpanId.ToString(CultureInfo.InvariantCulture);
        }

        public SpanContext Extract()
        {
            ulong? traceId = _headers[HttpHeaderNames.TraceId]?.TryParseUInt64(NumberStyles.Integer, CultureInfo.InvariantCulture);
            ulong? parentId = _headers[HttpHeaderNames.ParentId]?.TryParseUInt64(NumberStyles.Integer, CultureInfo.InvariantCulture);

            return traceId != null && parentId != null
                       ? new SpanContext(traceId.Value, parentId.Value)
                       : null;
        }
    }
}
