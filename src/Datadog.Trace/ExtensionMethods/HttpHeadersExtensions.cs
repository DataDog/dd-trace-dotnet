using System;
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
        /// Creates a <see cref="SpanContext"/> from the values found in this <see cref="HttpHeaders"/>.
        /// </summary>
        /// <param name="headers">The HTTP headers that contain the values to be extracted.</param>
        /// <returns>A new <see cref="SpanContext"/> that contains values extracted from <paramref name="headers"/>.</returns>
        public static SpanContext Extract(this HttpHeaders headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

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
        /// Adds new HTTP headers to this <see cref="HttpHeaders"/> with the values found in the specified <see cref="SpanContext"/>.
        /// </summary>
        /// <param name="headers">The <see cref="HttpHeaders"/> to add new headers to.</param>
        /// <param name="context">The <see cref="SpanContext"/> that contains the values to be added as HTTP headers.</param>
        public static void Inject(this HttpHeaders headers, SpanContext context)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            headers.Remove(HttpHeaderNames.TraceId);
            headers.Remove(HttpHeaderNames.ParentId);

            if (context != null)
            {
                headers.Add(HttpHeaderNames.TraceId, context.TraceId.ToString(CultureInfo.InvariantCulture));
                headers.Add(HttpHeaderNames.ParentId, context.SpanId.ToString(CultureInfo.InvariantCulture));

                if (context.SamplingPriority != null)
                {
                    var samplingPriority = (int)context.SamplingPriority;
                    headers.Add(HttpHeaderNames.SamplingPriority, samplingPriority.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }
}
