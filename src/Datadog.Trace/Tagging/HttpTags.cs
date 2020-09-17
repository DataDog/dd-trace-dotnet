namespace Datadog.Trace.Tagging
{
    internal class HttpTags : TagsDictionary
    {
        private const string HttpClientHandlerTypeKey = "http-client-handler-type";

        private static readonly Property<HttpTags, string>[] TagsProperties =
        {
            new Property<HttpTags, string>(Tags.HttpStatusCode, t => t.HttpStatusCode, (t, v) => t.HttpStatusCode = v),
            new Property<HttpTags, string>(HttpClientHandlerTypeKey, t => t.HttpClientHandlerType, (t, v) => t.HttpClientHandlerType = v),
            new Property<HttpTags, string>(Tags.SpanKind, t => t.SpanKind, (t, v) => t.SpanKind = v),
            new Property<HttpTags, string>(Tags.HttpMethod, t => t.HttpMethod, (t, v) => t.HttpMethod = v),
            new Property<HttpTags, string>(Tags.HttpUrl, t => t.HttpUrl, (t, v) => t.HttpUrl = v),
            new Property<HttpTags, string>(Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v)
        };

        private static readonly Property<HttpTags, double?>[] MetricsProperties =
        {
            new Property<HttpTags, double?>(Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v),
            new Property<HttpTags, double?>(Trace.Metrics.SamplingLimitDecision, t => t.SamplingLimitDecision, (t, v) => t.SamplingLimitDecision = v),
            new Property<HttpTags, double?>(Trace.Metrics.SamplingPriority, t => t.SamplingPriority, (t, v) => t.SamplingPriority = v)
        };

        public string SpanKind { get; set; }

        public string HttpMethod { get; set; }

        public string HttpUrl { get; set; }

        public string InstrumentationName { get; set; }

        public string HttpClientHandlerType { get; set; }

        public string HttpStatusCode { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        public double? SamplingLimitDecision { get; set; }

        public double? SamplingPriority { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => TagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => MetricsProperties;
    }
}
