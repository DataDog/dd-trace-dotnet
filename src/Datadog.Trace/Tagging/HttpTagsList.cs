using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class HttpTagsList : ExtendedCommonTagsList
    {
        private const string HttpClientHandlerTypeKey = "http-client-handler-type";

        private static new readonly IProperty<string>[] TagsProperties =
            ExtendedCommonTagsList.TagsProperties.Concat(
                new Property<HttpTagsList, string>(Trace.Tags.Env, t => t.Environment, (t, v) => t.Environment = v),
                new Property<HttpTagsList, string>(Trace.Tags.Version, t => t.Version, (t, v) => t.Version = v),
                new Property<HttpTagsList, string>(Trace.Tags.HttpStatusCode, t => t.HttpStatusCode, (t, v) => t.HttpStatusCode = v),
                new Property<HttpTagsList, string>(HttpClientHandlerTypeKey, t => t.HttpClientHandlerType, (t, v) => t.HttpClientHandlerType = v),
                new Property<HttpTagsList, string>(Trace.Tags.SpanKind, t => t.SpanKind, (t, v) => t.SpanKind = v),
                new Property<HttpTagsList, string>(Trace.Tags.HttpMethod, t => t.HttpMethod, (t, v) => t.HttpMethod = v),
                new Property<HttpTagsList, string>(Trace.Tags.HttpUrl, t => t.HttpUrl, (t, v) => t.HttpUrl = v),
                new Property<HttpTagsList, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v));

        private static new readonly IProperty<double?>[] MetricsProperties =
            ExtendedCommonTagsList.MetricsProperties.Concat(
                new Property<HttpTagsList, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v),
                new Property<HttpTagsList, double?>(Trace.Metrics.SamplingLimitDecision, t => t.SamplingLimitDecision, (t, v) => t.SamplingLimitDecision = v),
                new Property<HttpTagsList, double?>(Trace.Metrics.SamplingPriority, t => t.SamplingPriority, (t, v) => t.SamplingPriority = v));

        public string SpanKind { get; set; }

        public string HttpMethod { get; set; }

        public string HttpUrl { get; set; }

        public string InstrumentationName { get; set; }

        public string HttpClientHandlerType { get; set; }

        public string HttpStatusCode { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => TagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => MetricsProperties;
    }
}
