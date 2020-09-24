using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class RedisTags : CommonTags
    {
        private static new readonly IProperty<string>[] TagsProperties =
            CommonTags.TagsProperties.Concat(
                new Property<RedisTags, string>(Trace.Tags.RedisRawCommand, t => t.RawCommand, (t, v) => t.RawCommand = v),
                new Property<RedisTags, string>(Trace.Tags.OutPort, t => t.Port, (t, v) => t.Port = v),
                new Property<RedisTags, string>(Trace.Tags.OutHost, t => t.Host, (t, v) => t.Host = v));

        private static new readonly IProperty<double?>[] MetricsProperties =
            CommonTags.MetricsProperties.Concat(
                new Property<RedisTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        public string RawCommand { get; set; }

        public string Host { get; set; }

        public string Port { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => TagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => MetricsProperties;
    }
}
