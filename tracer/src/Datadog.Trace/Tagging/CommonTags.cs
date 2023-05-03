// <copyright file="CommonTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class CommonTags : TagsList
    {
        [Metric(Trace.Metrics.SamplingLimitDecision)]
        public double? SamplingLimitDecision { get; set; }

        [Metric(Trace.Metrics.SamplingRuleDecision)]
        public double? SamplingRuleDecision { get; set; }

        [Metric(Trace.Metrics.TracesKeepRate)]
        public double? TracesKeepRate { get; set; }

        [Metric(Trace.Metrics.SamplingAgentDecision)]
        public double? SamplingAgentDecision { get; set; }
    }
}
