// <copyright file="CommonTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class CommonTags : TagsList
    {
        [Tag(Trace.Tags.Env)]
        public string Environment { get; set; }

        [Tag(Trace.Tags.Version)]
        public string Version { get; set; }

        [Metric(Trace.Metrics.SamplingPriority)]
        public double? SamplingPriority { get; set; }

        [Metric(Trace.Metrics.SamplingLimitDecision)]
        public double? SamplingLimitDecision { get; set; }

        [Metric(Trace.Metrics.TracesKeepRate)]
        public double? TracesKeepRate { get; set; }

        public override void EnumerateTags<TProcessor>(TProcessor processor)
        {
            processor.Process(new TagItem<string>("env", Environment, EnvironmentBytes));
            processor.Process(new TagItem<string>("version", Version, VersionBytes));
            base.EnumerateTags(processor);
        }

        public override void EnumerateMetrics<TProcessor>(TProcessor processor)
        {
            if (SamplingPriority.HasValue)
            {
                processor.Process(new TagItem<double>("_sampling_priority_v1", SamplingPriority.Value, SamplingPriorityBytes));
            }

            if (SamplingLimitDecision.HasValue)
            {
                processor.Process(new TagItem<double>("_dd.limit_psr", SamplingLimitDecision.Value, SamplingLimitDecisionBytes));
            }

            if (TracesKeepRate.HasValue)
            {
                processor.Process(new TagItem<double>("_dd.tracer_kr", TracesKeepRate.Value, TracesKeepRateBytes));
            }

            base.EnumerateMetrics(processor);
        }
    }
}
