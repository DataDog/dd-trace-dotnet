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

        public void SetAnalyticsSampleRate(string integrationName, TracerSettings settings, bool enabledWithGlobalSetting)
        {
            if (integrationName != null)
            {
                var analyticsSampleRate = settings.GetIntegrationAnalyticsSampleRate(integrationName, enabledWithGlobalSetting);

                if (analyticsSampleRate != null)
                {
                    AnalyticsSampleRate = analyticsSampleRate;
                }
            }
        }

        protected override IProperty<string>[] GetAdditionalTags() => InstrumentationTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => InstrumentationMetricsProperties;
    }
}
