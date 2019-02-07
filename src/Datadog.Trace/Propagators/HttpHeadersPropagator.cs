using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Propagators
{
    internal class HttpHeadersPropagator : IPropagator
    {
        private readonly HttpHeaders _headers;

        public HttpHeadersPropagator(HttpHeaders headers)
        {
            _headers = headers;
        }

        public void Inject(SpanContext context)
        {
            _headers.Remove(HttpHeaderNames.TraceId);
            _headers.Add(HttpHeaderNames.TraceId, context.TraceId.ToString(CultureInfo.InvariantCulture));

            _headers.Remove(HttpHeaderNames.ParentId);
            _headers.Add(HttpHeaderNames.ParentId, context.SpanId.ToString(CultureInfo.InvariantCulture));
        }

        public SpanContext Extract()
        {
            ulong? traceId = null;
            ulong? parentId = null;

            if (_headers.TryGetValues(HttpHeaderNames.TraceId, out var traceIds))
            {
                traceId = traceIds.FirstOrDefault()?.TryParseUInt64(NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            if (_headers.TryGetValues(HttpHeaderNames.ParentId, out var parentIds))
            {
                parentId = parentIds.FirstOrDefault()?.TryParseUInt64(NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            return traceId != null && parentId != null
                       ? new SpanContext(traceId.Value, parentId.Value)
                       : null;
        }
    }
}
