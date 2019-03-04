using System;
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
        /// Creates a <see cref="SpanContext"/> from the values found in this <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="collection">The name/value pairs that contain the values to be extracted.</param>
        /// <returns>A new <see cref="SpanContext"/> that contains values extracted from <paramref name="collection"/>.</returns>
        public static SpanContext Extract(this NameValueCollection collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (ulong.TryParse(collection[HttpHeaderNames.TraceId], NumberStyles.Integer, CultureInfo.InvariantCulture, out var traceId) &&
                ulong.TryParse(collection[HttpHeaderNames.ParentId], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentId))
            {
                return new SpanContext(traceId, parentId);
            }

            return null;
        }

        /// <summary>
        /// Adds new name/value pairs to this <see cref="NameValueCollection"/> with the values found in the specified <see cref="SpanContext"/>.
        /// </summary>
        /// <param name="collection">The <see cref="NameValueCollection"/> to add new name/value pairs to.</param>
        /// <param name="context">The <see cref="SpanContext"/> that contains the values to be added as name/value pairs.</param>
        public static void Inject(this NameValueCollection collection, SpanContext context)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (context == null)
            {
                collection.Remove(HttpHeaderNames.TraceId);
                collection.Remove(HttpHeaderNames.ParentId);
            }
            else
            {
                collection[HttpHeaderNames.TraceId] = context.TraceId.ToString(CultureInfo.InvariantCulture);
                collection[HttpHeaderNames.ParentId] = context.SpanId.ToString(CultureInfo.InvariantCulture);

                if (context.SamplingPriority != null)
                {
                    var samplingPriority = (int)context.SamplingPriority;
                    collection[HttpHeaderNames.SamplingPriority] = samplingPriority.ToString(CultureInfo.InvariantCulture);
                }
            }
        }
    }
}
