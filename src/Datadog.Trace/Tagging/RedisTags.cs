using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class RedisTags : CommonTags
    {
        internal static readonly IProperty<string>[] RedisTagsProperties =
            CommonTagsProperties.Concat(
                new Property<RedisTags, string>(Trace.Tags.RedisRawCommand, t => t.RawCommand, (t, v) => t.RawCommand = v),
                new Property<RedisTags, string>(Trace.Tags.OutPort, t => t.Port, (t, v) => t.Port = v),
                new Property<RedisTags, string>(Trace.Tags.OutHost, t => t.Host, (t, v) => t.Host = v));

        internal static readonly IProperty<double?>[] RedisMetricsProperties =
            CommonMetricsProperties.Concat(
                new Property<RedisTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        public string RawCommand { get; set; }

        public string Host { get; set; }

        public string Port { get; set; }

        public double? AnalyticsSampleRate { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => RedisTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => RedisMetricsProperties;
    }
}
