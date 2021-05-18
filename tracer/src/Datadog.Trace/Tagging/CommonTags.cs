// <copyright file="CommonTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tagging
{
    internal class CommonTags : TagsList
    {
        protected static readonly IProperty<double?>[] CommonMetricsProperties =
        {
            new Property<CommonTags, double?>(Trace.Metrics.SamplingLimitDecision, t => t.SamplingLimitDecision, (t, v) => t.SamplingLimitDecision = v),
            new Property<CommonTags, double?>(Trace.Metrics.SamplingPriority, t => t.SamplingPriority, (t, v) => t.SamplingPriority = v),
            new Property<CommonTags, double?>(Trace.Metrics.TracesKeepRate, t => t.TracesKeepRate, (t, v) => t.TracesKeepRate = v)
        };

        protected static readonly IProperty<string>[] CommonTagsProperties =
        {
            new Property<CommonTags, string>(Trace.Tags.Env, t => t.Environment, (t, v) => t.Environment = v),
            new Property<CommonTags, string>(Trace.Tags.Version, t => t.Version, (t, v) => t.Version = v)
        };

        public string Environment { get; set; }

        public string Version { get; set; }

        public double? SamplingPriority { get; set; }

        public double? SamplingLimitDecision { get; set; }

        public double? TracesKeepRate { get; set; }

        protected override IProperty<double?>[] GetAdditionalMetrics() => CommonMetricsProperties;

        protected override IProperty<string>[] GetAdditionalTags() => CommonTagsProperties;
    }
}
