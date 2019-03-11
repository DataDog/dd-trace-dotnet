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

            ulong traceId = 0;
            ulong parentId = 0;
            SamplingPriority? samplingPriority = null;

            if (headers.TryGetValue(HttpHeaderNames.TraceId, out var traceIdHeaders))
            {
                ulong.TryParse(traceIdHeaders.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out traceId);
            }

            if (traceId == 0)
            {
                // a valid traceId is required to use distributed tracing
                return null;
            }

            if (headers.TryGetValue(HttpHeaderNames.ParentId, out var parentIdHeaders))
            {
                ulong.TryParse(parentIdHeaders.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parentId);
            }

            if (headers.TryGetValue(HttpHeaderNames.SamplingPriority, out var samplingPriorityHeaders) &&
                int.TryParse(samplingPriorityHeaders.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var samplingPriorityValue))
            {
                samplingPriority = (SamplingPriority?)samplingPriorityValue;
            }

            return new SpanContext(traceId, parentId, samplingPriority);
        }
    }
}
