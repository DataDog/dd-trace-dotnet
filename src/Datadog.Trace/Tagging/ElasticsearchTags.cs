using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class ElasticsearchTags : CommonTags
    {
        internal static readonly IProperty<string>[] ElasticsearchTagsProperties =
            CommonTagsProperties.Concat(
                new Property<ElasticsearchTags, string>(Trace.Tags.SpanKind, t => t.SpanKind, (t, v) => t.SpanKind = v),
                new Property<ElasticsearchTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v),
                new Property<ElasticsearchTags, string>(Trace.Tags.ElasticsearchAction, t => t.Action, (t, v) => t.Action = v),
                new Property<ElasticsearchTags, string>(Trace.Tags.ElasticsearchMethod, t => t.Method, (t, v) => t.Method = v),
                new Property<ElasticsearchTags, string>(Trace.Tags.ElasticsearchUrl, t => t.Url, (t, v) => t.Url = v));

        internal static readonly IProperty<double?>[] ElasticsearchMetricsProperties =
            CommonMetricsProperties.Concat(
                new Property<ElasticsearchTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        public string SpanKind { get; set; }

        public string InstrumentationName { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        public string Action { get; set; }

        public string Method { get; set; }

        public string Url { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => ElasticsearchTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => ElasticsearchMetricsProperties;
    }
}
