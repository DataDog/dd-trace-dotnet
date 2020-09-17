namespace Datadog.Trace.Tagging
{
    internal class CommonTags : TagsDictionary
    {
        private static readonly Property<CommonTags, double?>[] MetricsProperties =
        {
            new Property<CommonTags, double?>(Trace.Metrics.SamplingLimitDecision, t => t.SamplingLimitDecision, (t, v) => t.SamplingLimitDecision = v),
            new Property<CommonTags, double?>(Trace.Metrics.SamplingPriority, t => t.SamplingPriority, (t, v) => t.SamplingPriority = v)
        };

        public double? SamplingPriority { get; set; }

        public double? SamplingLimitDecision { get; set; }

        protected override IProperty<double?>[] GetAdditionalMetrics() => MetricsProperties;
    }
}
