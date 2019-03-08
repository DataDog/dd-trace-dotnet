using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class HttpSpanTagsProducer
    {
        private HttpSpanTagsProducer()
        {
        }

        public static HttpSpanTagsProducer Instance { get; } = new HttpSpanTagsProducer();

        public IEnumerable<KeyValuePair<string, string>> GetTags(IHttpSpanTagsSource from)
        {
            // If the time comes for use to need flexibility in terms of producing different tags for different sources/etc.,
            // this could/should be turned into a strategy abstraction or similar...for now...
            yield return new KeyValuePair<string, string>(Tags.HttpMethod, from.GetHttpMethod()?.ToUpperInvariant() ?? "GET");

            yield return new KeyValuePair<string, string>(Tags.HttpRequestHeadersHost, from.GetHttpHost());

            yield return new KeyValuePair<string, string>(Tags.HttpUrl, from.GetHttpUrl()?.ToLowerInvariant() ?? string.Empty);
        }
    }
}
