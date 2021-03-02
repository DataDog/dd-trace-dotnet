using System;
using System.Collections.Generic;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AspNet
{
    internal static class SpanExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanExtensions));

        internal static void ApplyHeaderTags(this Span span, IHeadersCollection headers, IDictionary<string, string> headerTags)
        {
            if (headerTags is not null && !headerTags.IsEmpty())
            {
                try
                {
                    // extract propagation details from http headers
                    var tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headers, headerTags);
                    foreach (KeyValuePair<string, string> kvp in tagsFromHeaders)
                    {
                        span.SetTag(kvp.Key, kvp.Value);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }
        }
    }
}
