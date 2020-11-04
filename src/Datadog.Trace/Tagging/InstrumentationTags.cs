using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal abstract class InstrumentationTags : CommonTags
    {
        protected static readonly IProperty<string>[] InstrumentationTagsProperties =
            CommonTagsProperties.Concat(
                new ReadOnlyProperty<InstrumentationTags, string>(Trace.Tags.SpanKind, t => t.SpanKind));

        protected static readonly IProperty<double?>[] InstrumentationMetricsProperties =
            CommonMetricsProperties.Concat(
                new Property<InstrumentationTags, double?>(Trace.Tags.Analytics, t => t.AnalyticsSampleRate, (t, v) => t.AnalyticsSampleRate = v));

        public abstract string SpanKind { get; }

        public double? AnalyticsSampleRate { get; set; }

        public void SetAnalyticsSampleRate(int integrationId, TracerSettings settings, bool enabledWithGlobalSetting)
        {
            if (settings != null)
            {
                AnalyticsSampleRate = settings.GetIntegrationAnalyticsSampleRate(integrationId, enabledWithGlobalSetting);
            }
        }

        protected override IProperty<string>[] GetAdditionalTags() => InstrumentationTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => InstrumentationMetricsProperties;
    }
}
