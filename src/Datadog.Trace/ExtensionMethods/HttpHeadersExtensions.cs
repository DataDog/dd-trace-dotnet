using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="HttpHeaders"/>.
    /// </summary>
    public static class HttpHeadersExtensions
    {
        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the specified headers.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>The <see cref="SpanContext"/></returns>
        public static SpanContext Extract(this HttpHeaders headers)
        {
            ulong? traceId = null;
            ulong? parentId = null;

            if (headers.TryGetValues(HttpHeaderNames.TraceId, out var traceIds))
            {
                traceId = traceIds.FirstOrDefault()?.TryParseUInt64(NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            if (headers.TryGetValues(HttpHeaderNames.ParentId, out var parentIds))
            {
                parentId = parentIds.FirstOrDefault()?.TryParseUInt64(NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            return traceId != null && parentId != null
                       ? new SpanContext(traceId.Value, parentId.Value)
                       : null;
        }

        /// <summary>
        /// Injects the specified <see cref="SpanContext"/> in the <see cref="HttpHeaders"/>.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <param name="context">The context.</param>
        public static void Inject(this HttpHeaders headers, SpanContext context)
        {
            headers.Remove(HttpHeaderNames.TraceId);
            headers.Add(HttpHeaderNames.TraceId, context.TraceId.ToString(CultureInfo.InvariantCulture));

            headers.Remove(HttpHeaderNames.ParentId);
            headers.Add(HttpHeaderNames.ParentId, context.SpanId.ToString(CultureInfo.InvariantCulture));
        }
    }
}
