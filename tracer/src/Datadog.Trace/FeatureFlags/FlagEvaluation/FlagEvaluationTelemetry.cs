// <copyright file="FlagEvaluationTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal sealed class FlagEvaluationTelemetry(IMetricsTelemetryCollector metrics)
{
    private readonly IMetricsTelemetryCollector _metrics = metrics;

    internal void Dropped(MetricTags.FlagEvaluationDropReason reason, long count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        try
        {
            _metrics.RecordCountFlagEvaluationRowsDropped(reason, Increment(count));
        }
        catch (Exception)
        {
            // Telemetry is best-effort, never part of evaluation correctness.
        }
    }

    internal void Degraded(MetricTags.FlagEvaluationDegradeReason reason, long count)
    {
        if (count <= 0)
        {
            return;
        }

        try
        {
            _metrics.RecordCountFlagEvaluationRowsDegraded(reason, Increment(count));
        }
        catch (Exception)
        {
        }
    }

    internal void InvalidTargetingKeys(long count)
    {
        if (count <= 0)
        {
            return;
        }

        try
        {
            _metrics.RecordCountFlagEvaluationTargetingKeyOmitted(MetricTags.FlagEvaluationTargetingReason.Invalid, Increment(count));
        }
        catch (Exception)
        {
        }
    }

    internal void PayloadSplits(int count)
    {
        if (count <= 0)
        {
            return;
        }

        try
        {
            _metrics.RecordCountFlagEvaluationPayloadSplits(count);
        }
        catch (Exception)
        {
        }
    }

    internal void ContextOmissions(ContextOmissionReason reasons)
    {
        for (var bit = 0; bit <= (int)MetricTags.FlagEvaluationContextReason.SnapshotError; bit++)
        {
            if (((int)reasons & (1 << bit)) == 0)
            {
                continue;
            }

            try
            {
                _metrics.RecordCountFlagEvaluationContextTruncated((MetricTags.FlagEvaluationContextReason)bit);
            }
            catch (Exception)
            {
            }
        }
    }

    internal void HookError()
    {
        try
        {
            _metrics.RecordCountFlagEvaluationHookErrors();
        }
        catch (Exception)
        {
        }
    }

    // The shared collector accepts int increments. Keep best-effort metrics nonnegative
    // for exceptionally large batches without changing long-valued payload counts.
    private static int Increment(long count) => (int)Math.Min(count, int.MaxValue);
}
