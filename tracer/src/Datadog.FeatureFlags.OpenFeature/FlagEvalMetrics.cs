// <copyright file="FlagEvalMetrics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// Manages OTel metric instruments for flag evaluation tracking.
/// Uses System.Diagnostics.Metrics which is the standard .NET metrics API.
/// </summary>
internal sealed class FlagEvalMetrics : IDisposable
{
    internal const string MeterName = "Datadog.FeatureFlags.OpenFeature";
    internal const string MetricName = "feature_flag.evaluations";
    internal const string MetricUnit = "{evaluation}";
    internal const string MetricDescription = "Number of feature flag evaluations";

    internal const string TagFlagKey = "feature_flag.key";
    internal const string TagVariant = "feature_flag.result.variant";
    internal const string TagReason = "feature_flag.result.reason";
    internal const string TagErrorType = "error.type";
    internal const string TagAllocationKey = "feature_flag.result.allocation_key";

    internal const string MetadataAllocationKey = "dd_allocationKey";

    private readonly Meter _meter;
    private readonly Counter<long> _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlagEvalMetrics"/> class.
    /// </summary>
    public FlagEvalMetrics()
    {
        _meter = new Meter(MeterName);
        _counter = _meter.CreateCounter<long>(
            MetricName,
            unit: MetricUnit,
            description: MetricDescription);
    }

    /// <summary>
    /// Records a single flag evaluation metric with the specified attributes.
    /// </summary>
    /// <param name="flagKey">The flag key that was evaluated.</param>
    /// <param name="variant">The resolved variant, or empty string if none.</param>
    /// <param name="reason">The evaluation reason (e.g., "targeting_match", "error").</param>
    /// <param name="errorType">The error type if reason is error, or null.</param>
    /// <param name="allocationKey">The allocation key if available, or null.</param>
    public void Record(
        string flagKey,
        string? variant,
        string reason,
        string? errorType,
        string? allocationKey)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(TagFlagKey, flagKey),
            new(TagVariant, variant ?? string.Empty),
            new(TagReason, reason)
        };

        if (!string.IsNullOrEmpty(errorType))
        {
            tags.Add(new(TagErrorType, errorType));
        }

        if (!string.IsNullOrEmpty(allocationKey))
        {
            tags.Add(new(TagAllocationKey, allocationKey));
        }

        _counter.Add(1, tags.ToArray());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _meter.Dispose();
    }
}
#endif
