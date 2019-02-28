using System.Collections.Specialized;
using System.Globalization;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="NameValueCollection"/>.
    /// </summary>
    public static class NameValueCollectionExtensions
    {
        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the specified <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="headers">A collection of string names and value, such as HTTP headers.</param>
        /// <returns>The <see cref="SpanContext"/></returns>
        public static SpanContext Extract(this NameValueCollection headers)
        {
            if (ulong.TryParse(headers[HttpHeaderNames.TraceId], NumberStyles.Integer, CultureInfo.InvariantCulture, out var traceId) &&
                ulong.TryParse(headers[HttpHeaderNames.ParentId], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentId))
            {
                return new SpanContext(traceId, parentId);
            }

            return null;
        }

        /// <summary>
        /// Injects a <see cref="SpanContext"/> into the specified <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="headers">A collection of string names and value, such as HTTP headers.</param>
        /// <param name="context">The context.</param>
        public static void Inject(this NameValueCollection headers, SpanContext context)
        {
            headers[HttpHeaderNames.TraceId] = context.TraceId.ToString(CultureInfo.InvariantCulture);
            headers[HttpHeaderNames.ParentId] = context.SpanId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
