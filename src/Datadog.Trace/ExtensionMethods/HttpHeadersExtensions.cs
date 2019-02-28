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
            if (headers.TryGetValues(HttpHeaderNames.TraceId, out var traceIds) &&
                headers.TryGetValues(HttpHeaderNames.ParentId, out var parentIds) &&
                ulong.TryParse(traceIds.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var traceId) &&
                ulong.TryParse(parentIds.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentId))
            {
                return new SpanContext(traceId, parentId);
            }

            return null;
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
