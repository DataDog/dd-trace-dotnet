namespace Datadog.Trace.Tagging
{
    internal class CommonTagsList : TagsList
    {
        private static readonly Property<CommonTagsList, double?>[] MetricsProperties =
        {
            new Property<CommonTagsList, double?>(Trace.Metrics.SamplingLimitDecision, t => t.SamplingLimitDecision, (t, v) => t.SamplingLimitDecision = v),
            new Property<CommonTagsList, double?>(Trace.Metrics.SamplingPriority, t => t.SamplingPriority, (t, v) => t.SamplingPriority = v)
        };

        public double? SamplingPriority { get; set; }

        public double? SamplingLimitDecision { get; set; }

        protected override IProperty<double?>[] GetAdditionalMetrics() => MetricsProperties;
    }
}
