using System;
using System.Globalization;
using System.Linq;
using Datadog.Trace;
using Microsoft.AspNetCore.Http;

namespace Samples.AspNetCoreMvc2.Extensions
{
    public static class HeaderDictionaryExtensions
    {
        public static SpanContext Extract(this IHeaderDictionary headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            if (headers.TryGetValue(HttpHeaderNames.TraceId, out var traceIds) &&
                headers.TryGetValue(HttpHeaderNames.ParentId, out var parentIds) &&
                ulong.TryParse(traceIds.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var traceId) &&
                ulong.TryParse(parentIds.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentId))
            {
                return new SpanContext(traceId, parentId, SamplingPriority.UserKeep);
            }

            return null;
        }
    }
}
