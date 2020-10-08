using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class WebTags : CommonTags
    {
        internal static readonly IProperty<string>[] WebTagsProperties =
            CommonTagsProperties.Concat(
                new Property<WebTags, string>(Trace.Tags.SpanKind, t => t.SpanKind, (t, v) => t.SpanKind = v),
                new Property<WebTags, string>(Trace.Tags.HttpStatusCode, t => t.StatusCode, (t, v) => t.StatusCode = v),
                new Property<WebTags, string>(Trace.Tags.HttpMethod, t => t.HttpMethod, (t, v) => t.HttpMethod = v),
                new Property<WebTags, string>(Trace.Tags.HttpRequestHeadersHost, t => t.HttpRequestHeadersHost, (t, v) => t.HttpRequestHeadersHost = v),
                new Property<WebTags, string>(Trace.Tags.HttpUrl, t => t.HttpUrl, (t, v) => t.HttpUrl = v),
                new Property<WebTags, string>(Trace.Tags.Language, t => t.Language, (t, v) => t.Language = v));

        internal static readonly IProperty<double?>[] WebMetricsProperties =
            CommonMetricsProperties.Concat(
                new Property<WebTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        public string SpanKind { get; set; }

        public string HttpMethod { get; set; }

        public string HttpRequestHeadersHost { get; set; }

        public string HttpUrl { get; set; }

        public string Language { get; set; }

        public string StatusCode { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => WebTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => WebMetricsProperties;
    }
}
