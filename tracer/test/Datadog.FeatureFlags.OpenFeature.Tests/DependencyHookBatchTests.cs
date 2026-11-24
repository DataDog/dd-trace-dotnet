// <copyright file="DependencyHookBatchTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class DependencyHookBatchTests
{
    [Fact]
    public void DependencyTelemetryCopiesReadOnlyMaps()
    {
        var source = new Dictionary<string, object?> { ["label"] = "original" };
        var record = Dependency("parent", exposureEligible: false);
        record["contextAttributes"] = new ReadOnlyDictionary<string, object?>(source);
        record["evaluationFeatures"] = new ReadOnlyDictionary<string, object?>(source);

        var resolution = Resolution("root", error: null, [record]);
        var dependency = FeatureFlagsSdk.GetResult(resolution.FlagMetadata)!.DependencyEvaluations!.Single();
        source["label"] = "mutated";

        dependency.ContextAttributes.Should().Contain("label", "original");
        dependency.EvaluationFeatures.Should().Contain("label", "original");
        var mutateContext = () => ((IDictionary<string, object?>)dependency.ContextAttributes!)["changed"] = true;
        var mutateFeatures = () => ((IDictionary<string, object?>)dependency.EvaluationFeatures!)["changed"] = true;
        mutateContext.Should().Throw<NotSupportedException>();
        mutateFeatures.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task CommittedDependencyExposuresRunOnceAndCallbackFailureIsIsolated()
    {
        var records = new List<IDictionary<string, object?>>
        {
            Dependency("first", exposureEligible: true, featureValue: "one"),
            Dependency("second", exposureEligible: true, featureValue: "two"),
            Dependency("evaluated-only", exposureEligible: false),
        };
        var resolution = Resolution("root", error: null, records);
        var state = FeatureFlagsSdk.GetResult(resolution.FlagMetadata)!;
        state.EvaluatedContext = Context();
        FeatureFlagsSdk.CommitPendingExposure(
            resolution.FlagMetadata,
            _ =>
            {
                records[0]["exposureCommitted"] = true;
                records[0]["exposureCacheHit"] = false;
                records[1]["exposureCommitted"] = true;
                records[1]["exposureCacheHit"] = true;
                return ExposureCommitResult.New;
            });

        var attempted = new List<string>();
        var hook = new ExposureHook(evaluation =>
        {
            attempted.Add(evaluation.FlagKey);
            if (evaluation.FlagKey == "first")
            {
                throw new InvalidOperationException("callback failure");
            }

            if (evaluation.FlagKey == "second")
            {
                var mutation = () => ((IDictionary<string, object>)evaluation.Features)["changed"] = true;
                mutation.Should().Throw<NotSupportedException>();
                evaluation.IsCacheHit.Should().BeTrue();
                evaluation.SplitSerialId.Should().Be(7);
            }
        });

        await hook.FinallyAsync(Context("root"), Details(resolution));

        attempted.Should().BeEquivalentTo("first", "second", "root");
    }

    [Theory]
    [CombinatorialData]
    public async Task DependencyExposureCallbacksRequireSuccessfulRootAndCommit(bool rootError)
    {
        var records = new List<IDictionary<string, object?>> { Dependency("parent", exposureEligible: true) };
        var resolution = Resolution("root", rootError ? "GENERAL" : null, records);
        if (rootError)
        {
            FeatureFlagsSdk.CommitPendingExposure(resolution.FlagMetadata, _ => ExposureCommitResult.New);
        }

        var received = new List<string>();
        var hook = new ExposureHook(evaluation => received.Add(evaluation.FlagKey));

        await hook.FinallyAsync(Context("root"), Details(resolution));

        received.Should().ContainSingle().Which.Should().Be("root", "a failed or uncommitted root must publish no dependency exposures");
    }

#if NET6_0_OR_GREATER
    [Fact]
    public async Task DependencyMetricsIncludeFailuresOnce()
    {
        var prefix = $"dependency-metric-{Guid.NewGuid():N}";
        var records = new List<IDictionary<string, object?>>
        {
            Dependency(prefix + "-success", exposureEligible: false),
            Dependency(prefix + "-failure", exposureEligible: false, errorCode: "FLAG_NOT_FOUND"),
        };
        var resolution = Resolution(prefix + "-root", "GENERAL", records);
        using var metrics = new MetricCapture(prefix);
        using var hook = new FlagEvalMetricsHook();

        await hook.AfterAsync(Context(prefix + "-root"), Details(resolution));
        await hook.FinallyAsync(Context(prefix + "-root"), Details(resolution));

        metrics.Measurements.Select(m => m.Key).Should().BeEquivalentTo(prefix + "-success", prefix + "-failure", prefix + "-root");
        metrics.Measurements.Single(m => m.Key == prefix + "-success").Error.Should().BeNull();
        metrics.Measurements.Single(m => m.Key == prefix + "-failure").Error.Should().Be("flag_not_found");
        metrics.Measurements.Single(m => m.Key == prefix + "-root").Error.Should().Be("general");
    }

    [Fact]
    public async Task ApplicationAfterFailureRetainsDependencyMetricsAndDiscardsExposures()
    {
        var prefix = $"after-failure-dependency-metric-{Guid.NewGuid():N}";
        var commits = 0;
        var records = new List<IDictionary<string, object?>>
        {
            Dependency(prefix + "-parent", exposureEligible: true),
        };
        var metadata = new Dictionary<string, string>
        {
            [FeatureFlagMetadataKeys.PendingExposure] = "true",
            [FeatureFlagMetadataKeys.DoLog] = "false",
        };
        using var metrics = new MetricCapture(prefix);
        using var provider = new DatadogProvider(
            (key, _, _, _, _) => new TestEvaluation(key, null, metadata),
            (key, _, _, _, _) => new TestEvaluation(key, null, metadata),
            () => true,
            _ =>
            {
                Interlocked.Increment(ref commits);
                return ExposureCommitResult.New;
            },
            _ => records);
        var domain = $"dependency-after-failure-{Guid.NewGuid():N}";
        await Api.Instance.SetProviderAsync(domain, provider);
        var client = Api.Instance.GetClient(domain);
        client.AddHooks(new FailingAfterHook());

        var result = await client.GetBooleanDetailsAsync(prefix + "-root", false);

        result.ErrorType.Should().Be(ErrorType.General);
        metrics.Measurements.Should().ContainSingle(m => m.Key == prefix + "-parent" && m.Error == null);
        metrics.Measurements.Should().ContainSingle(m => m.Key == prefix + "-root" && m.Error == "general");
        Volatile.Read(ref commits).Should().Be(0);
    }

    [Fact]
    public async Task ParallelRootsKeepTheirDependencyMetricBatchesIsolated()
    {
        var prefix = $"parallel-dependency-metric-{Guid.NewGuid():N}";
        using var metrics = new MetricCapture(prefix);
        using var hook = new FlagEvalMetricsHook();
        var tasks = Enumerable.Range(0, 16).Select(async index =>
        {
            var root = $"{prefix}-root-{index}";
            var resolution = Resolution(root, null, [Dependency($"{prefix}-dependency-{index}", exposureEligible: false)]);
            await hook.FinallyAsync(Context(root), Details(resolution));
        });

        await Task.WhenAll(tasks);

        metrics.Measurements.Should().HaveCount(32);
        metrics.Measurements.Select(m => m.Key).Distinct().Should().HaveCount(32);
    }
#endif

    private static ResolutionDetails<bool> Resolution(string flagKey, string? error, IReadOnlyList<IDictionary<string, object?>> records)
    {
        var metadata = new Dictionary<string, string>
        {
            [FeatureFlagMetadataKeys.PendingExposure] = "true",
            [FeatureFlagMetadataKeys.DoLog] = "true",
            [FeatureFlagMetadataKeys.AllocationKey] = "root-allocation",
        };
        var evaluation = new TestEvaluation(flagKey, error, metadata);
        return FeatureFlagsSdk.GetResolutionDetails(flagKey, false, evaluation, evalTimeMs: null, deferredExposure: true, records);
    }

    private static Dictionary<string, object?> Dependency(string flagKey, bool exposureEligible, string? featureValue = null, string? errorCode = null)
    {
        var features = new Dictionary<string, object?>();
        if (featureValue is not null)
        {
            features["label"] = featureValue;
        }

        return new Dictionary<string, object?>
        {
            ["flagKey"] = flagKey,
            ["variant"] = errorCode is null ? "on" : null,
            ["allocationKey"] = errorCode is null ? flagKey + "-allocation" : null,
            ["reason"] = (int)(errorCode is null ? EvaluationReason.TargetingMatch : EvaluationReason.Error),
            ["errorCode"] = errorCode,
            ["evaluationTimeMs"] = 123L,
            ["observeFullEvaluationData"] = true,
            ["targetingKey"] = "subject",
            ["contextOmissions"] = 0,
            ["hookFeatures"] = features,
            ["evaluationFeatures"] = features,
            ["exposureEligible"] = exposureEligible,
            ["exposureCommitted"] = false,
            ["serialId"] = 7L,
        };
    }

    private static HookContext<bool> Context(string flagKey = "root")
        => new(flagKey, false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), Context());

    private static EvaluationContext Context()
        => EvaluationContext.Builder().SetTargetingKey("subject").Build();

    private static FlagEvaluationDetails<bool> Details(ResolutionDetails<bool> resolution)
        => new(resolution.FlagKey, resolution.Value, resolution.ErrorType, resolution.Reason, resolution.Variant, resolution.ErrorMessage, resolution.FlagMetadata);

    private sealed class TestEvaluation : IEvaluation
    {
        public TestEvaluation(string flagKey, string? error, IDictionary<string, string> metadata)
        {
            FlagKey = flagKey;
            Value = error is null;
            Reason = error is null ? EvaluationReason.TargetingMatch : EvaluationReason.Error;
            Variant = error is null ? "on" : null;
            Error = error;
            FlagMetadata = metadata;
        }

        public string FlagKey { get; }

        public object? Value { get; }

        public EvaluationReason Reason { get; }

        public string? Variant { get; }

        public string? Error { get; }

        public IDictionary<string, string>? FlagMetadata { get; }
    }

    private sealed class FailingAfterHook : Hook
    {
        public override ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("after failed");
    }

#if NET6_0_OR_GREATER
    private sealed class MetricCapture : IDisposable
    {
        private readonly string _prefix;
        private readonly MeterListener _listener = new();
        private readonly List<(string Key, string? Error)> _measurements = [];

        internal MetricCapture(string prefix)
        {
            _prefix = prefix;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == FlagEvalMetrics.MeterName && instrument.Name == FlagEvalMetrics.MetricName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string? key = null;
                string? error = null;
                foreach (var tag in tags)
                {
                    key = tag.Key == FlagEvalMetrics.TagFlagKey ? tag.Value as string : key;
                    error = tag.Key == FlagEvalMetrics.TagErrorType ? tag.Value as string : error;
                }

                if (key?.StartsWith(_prefix, StringComparison.Ordinal) == true)
                {
                    lock (_measurements)
                    {
                        _measurements.Add((key, error));
                    }
                }
            });
            _listener.Start();
        }

        internal IReadOnlyList<(string Key, string? Error)> Measurements
        {
            get
            {
                lock (_measurements)
                {
                    return _measurements.ToArray();
                }
            }
        }

        public void Dispose() => _listener.Dispose();
    }
#endif
}
