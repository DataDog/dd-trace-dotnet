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
            ulong? traceId = headers[HttpHeaderNames.TraceId]?.TryParseUInt64(NumberStyles.Integer, CultureInfo.InvariantCulture);
            ulong? parentId = headers[HttpHeaderNames.ParentId]?.TryParseUInt64(NumberStyles.Integer, CultureInfo.InvariantCulture);

            return traceId != null && parentId != null
                       ? new SpanContext(traceId.Value, parentId.Value)
                       : null;
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
