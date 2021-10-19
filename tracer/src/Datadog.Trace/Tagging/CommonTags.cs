// <copyright file="CommonTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class CommonTags : TagsList
    {
        [TagName(Trace.Tags.Env)]
        public string Environment { get; set; }

        [TagName(Trace.Tags.Version)]
        public string Version { get; set; }

        [MetricName(Trace.Metrics.SamplingPriority)]
        public double? SamplingPriority { get; set; }

        [MetricName(Trace.Metrics.SamplingLimitDecision)]
        public double? SamplingLimitDecision { get; set; }

        [MetricName(Trace.Metrics.TracesKeepRate)]
        public double? TracesKeepRate { get; set; }
    }
}
