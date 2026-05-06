// <copyright file="FlagEvalMetricsHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Datadog.FeatureFlags.OpenFeature;
using FluentAssertions;
using global::OpenFeature;
using global::OpenFeature.Constant;
using global::OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

/// <summary>
/// Unit tests for <see cref="FlagEvalMetricsHook"/> using MeterListener to capture metric increments.
/// Tests the branching logic in FinallyAsync: error-type extraction, allocation-key extraction, unknown-reason fallback.
/// </summary>
public class FlagEvalMetricsHookTests : IDisposable
{
    private readonly MeterListener _meterListener;
    private readonly List<RecordedMeasurement> _measurements = new();
    private FlagEvalMetricsHook? _hook;

    public FlagEvalMetricsHookTests()
    {
        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == FlagEvalMetrics.MeterName &&
                instrument.Name == FlagEvalMetrics.MetricName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        _meterListener.Start();
    }

    public void Dispose()
    {
        _hook?.Dispose();
        _meterListener.Dispose();
    }

    [Fact]
    public async Task FinallyAsync_ExtractsErrorType_WhenPresent()
    {
        _hook = new FlagEvalMetricsHook();
        var context = CreateHookContext("error-flag");
        var details = CreateFlagEvaluationDetails("default", "error", ErrorType.FlagNotFound);

        await _hook.FinallyAsync(context, details);

        _measurements.Should().ContainSingle();
        _measurements[0].Tags.Should().Contain(FlagEvalMetrics.TagErrorType, "flag_not_found");
    }

    [Fact]
    public async Task FinallyAsync_ExtractsAllocationKey_FromMetadata()
    {
        _hook = new FlagEvalMetricsHook();
        var context = CreateHookContext("alloc-flag");
        var metadata = new ImmutableMetadata(
            new Dictionary<string, object> { { FlagEvalMetrics.MetadataAllocationKey, "alloc-123" } });
        var details = CreateFlagEvaluationDetails("variant-b", "split", flagMetadata: metadata);

        await _hook.FinallyAsync(context, details);

        _measurements.Should().ContainSingle();
        _measurements[0].Tags.Should().Contain(FlagEvalMetrics.TagAllocationKey, "alloc-123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task FinallyAsync_UsesUnknownReason_WhenReasonIsNullOrEmpty(string? reason)
    {
        _hook = new FlagEvalMetricsHook();
        var context = CreateHookContext("unknown-reason-flag");
        var details = CreateFlagEvaluationDetails("variant", reason);

        await _hook.FinallyAsync(context, details);

        _measurements.Should().ContainSingle();
        _measurements[0].Tags.Should().Contain(FlagEvalMetrics.TagReason, "unknown");
    }

    private void OnMeasurementRecorded(
        Instrument instrument,
        long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        var tagDict = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            tagDict[tag.Key] = tag.Value;
        }

        _measurements.Add(new RecordedMeasurement(measurement, tagDict));
    }

#pragma warning disable SA1204 // Static elements should appear before instance elements

    private static HookContext<string> CreateHookContext(string flagKey)
    {
        return new HookContext<string>(
            flagKey: flagKey,
            defaultValue: "default",
            flagValueType: FlagValueType.String,
            clientMetadata: new ClientMetadata(name: "test", version: "1.0"),
            providerMetadata: new Metadata(name: "test-provider"),
            evaluationContext: EvaluationContext.Empty);
    }

    private static FlagEvaluationDetails<string> CreateFlagEvaluationDetails(
        string? variant,
        string? reason,
        ErrorType errorType = ErrorType.None,
        ImmutableMetadata? flagMetadata = null)
    {
        return new FlagEvaluationDetails<string>(
            flagKey: "test",
            value: "test-value",
            errorType: errorType,
            reason: reason,
            variant: variant,
            errorMessage: null,
            flagMetadata: flagMetadata);
    }

#pragma warning restore SA1204 // Static elements should appear before instance elements

    private record RecordedMeasurement(long Value, Dictionary<string, object?> Tags);
}
