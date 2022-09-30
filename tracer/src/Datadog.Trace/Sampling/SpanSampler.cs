// <copyright file="SpanSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling;

/// <summary>
///     Represents a sampler for single span ingestion.
/// </summary>
internal class SpanSampler : ISpanSampler
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanSampler>();

    private readonly List<ISpanSamplingRule> _rules = new List<ISpanSamplingRule>();

    public SpanSampler(IEnumerable<ISpanSamplingRule> rules)
    {
        _rules = rules.ToList() ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <inheritdoc/>
    public void MakeSamplingDecision(Span span)
    {
        if (_rules.Count > 0)
        {
            foreach (var rule in _rules)
            {
                if (rule.ShouldKeep(span))
                {
                    Tag(span, rule);
                    return;
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Tag(Span span, ISpanSamplingRule rule)
    {
        // TODO do we want to tag here?
        // TODO maybe do a TryAdd? is SetTag safe?
        span.SetTag(Tags.SingleSpanSampling.RuleRate, rule.SamplingRate.ToString());

        if (rule.MaxPerSecond is not null)
        {
            span.SetTag(Tags.SingleSpanSampling.MaxPerSecond, rule.MaxPerSecond.ToString());
        }

        // TODO is this where we set this tag or should it be somewhere else?
        span.SetTag(Tags.SingleSpanSampling.SamplingMechanism, SamplingMechanism.SpanSamplingRule.ToString());
    }
}
