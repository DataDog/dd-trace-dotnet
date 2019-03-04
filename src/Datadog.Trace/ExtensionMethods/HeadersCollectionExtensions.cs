using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="IHeadersCollection"/>.
    /// </summary>
    public static class HeadersCollectionExtensions
    {
        private const NumberStyles NumberStyles = System.Globalization.NumberStyles.Integer;
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Creates a <see cref="SpanContext"/> from the values found in the specified headers.
        /// </summary>
        /// <param name="headers">The headers that contain the values to be extracted.</param>
        /// <returns>A new <see cref="SpanContext"/> that contains values extracted from <paramref name="headers"/>.</returns>
        public static SpanContext ExtractSpanContext(this IHeadersCollection headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            ulong traceId = ParseUInt64(headers.GetValues(HttpHeaderNames.TraceId));
            ulong parentId = ParseUInt64(headers.GetValues(HttpHeaderNames.ParentId));
            int samplingPriority = ParseInt32(headers.GetValues(HttpHeaderNames.SamplingPriority));

            return new SpanContext(traceId, parentId, (SamplingPriority)samplingPriority);
        }

        /// <summary>
        /// Adds new name/value pairs to this <see cref="NameValueCollection"/> with the values found in the specified <see cref="SpanContext"/>.
        /// </summary>
        /// <param name="headers">The <see cref="NameValueCollection"/> to add new name/value pairs to.</param>
        /// <param name="context">The <see cref="SpanContext"/> that contains the values to be added as name/value pairs.</param>
        public static void InjectSpanContext(this IHeadersCollection headers, SpanContext context)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            if (context != null)
            {
                headers.Set(HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));
                headers.Set(HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));

                if (context.SamplingPriority != null)
                {
                    var samplingPriority = (int)context.SamplingPriority;
                    headers.Set(HttpHeaderNames.SamplingPriority, samplingPriority.ToString(InvariantCulture));
                }
            }
        }

        private static ulong ParseUInt64(IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                if (ulong.TryParse(value, NumberStyles, InvariantCulture, out var result))
                {
                    return result;
                }
            }

            return 0;
        }

        private static int ParseInt32(IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                if (int.TryParse(value, NumberStyles, InvariantCulture, out var result))
                {
                    return result;
                }
            }

            return 0;
        }
    }
}
