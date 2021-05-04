// <copyright file="InstrumentationTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

        public void SetAnalyticsSampleRate(IntegrationInfo integration, TracerSettings settings, bool enabledWithGlobalSetting)
        {
            if (settings != null)
            {
                AnalyticsSampleRate = settings.GetIntegrationAnalyticsSampleRate(integration, enabledWithGlobalSetting);
            }
        }

        protected override IProperty<string>[] GetAdditionalTags() => InstrumentationTagsProperties;

        protected override IProperty<double?>[] GetAdditionalMetrics() => InstrumentationMetricsProperties;
    }
}
