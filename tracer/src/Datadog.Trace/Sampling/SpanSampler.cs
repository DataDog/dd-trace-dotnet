// <copyright file="SpanSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Sampling;

/// <summary>
///     Represents a sampler for single span ingestion.
/// </summary>
internal class SpanSampler : ISpanSampler
{
    private readonly List<ISpanSamplingRule> _rules;

    public SpanSampler(IEnumerable<ISpanSamplingRule> rules)
    {
        if (rules is null)
        {
            throw new ArgumentNullException(nameof(rules));
        }

        _rules = rules.ToList();
    }

    /// <inheritdoc/>
    public bool MakeSamplingDecision(Span span)
    {
        if (_rules.Count > 0)
        {
            foreach (var rule in _rules)
            {
                if (rule.IsMatch(span))
                {
                    if (rule.ShouldSample(span))
                    {
                        AddTags(span, rule);
                        return true;
                    }

                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Tags the <paramref name="span"/> with the necessary tags for single span ingestion.
    /// </summary>
    /// <param name="span">The <see cref="Span"/> to tag.</param>
    /// <param name="rule">The <see cref="ISpanSamplingRule"/> that contains the tag information.</param>
    private static void AddTags(Span span, ISpanSamplingRule rule)
    {
        span.Tags.SetMetric(Metrics.SingleSpanSampling.RuleRate, rule.SamplingRate);

        if (rule.MaxPerSecond is not null)
        {
            span.Tags.SetMetric(Metrics.SingleSpanSampling.MaxPerSecond, rule.MaxPerSecond);
        }

        span.Tags.SetMetric(Metrics.SingleSpanSampling.SamplingMechanism, SamplingMechanism.SpanSamplingRule);
    }
}
