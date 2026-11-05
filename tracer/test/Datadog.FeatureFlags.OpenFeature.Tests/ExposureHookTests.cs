// <copyright file="ExposureHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class ExposureHookTests
{
    [Fact]
    public async Task CallbackReceivesEveryCompletedEvaluation()
    {
        var received = new List<ExposureEvaluation>();
        var hook = new ExposureHook(e => received.Add(e));

        var success = await Evaluate(hook, error: null);
        var failure = await Evaluate(hook, error: "GENERAL");

        success.Should().BeTrue("the finally stage must complete normally");
        failure.Should().BeTrue("a failed evaluation still reaches the hook");
        received.Should().HaveCount(2, "the finally stage also runs for failed evaluations");
        received[0].FlagKey.Should().Be("flag");
        received[0].Variant.Should().Be("on");
        received[0].AllocationKey.Should().Be("alloc");
        received[0].TargetingKey.Should().Be("user-123");
        received[0].Context!.TargetingKey.Should().Be("user-123");
        received[1].IsExposure.Should().BeFalse("a failed evaluation is not an exposure");
    }

    [Fact]
    public async Task CallbackReceivesTheContextABeforeHookReturned()
    {
        var domain = $"exposure-context-{Guid.NewGuid():N}";
        string? evaluatedTargetingKey = null;
        using var provider = new DatadogProvider((flag, type, defaultValue, targetingKey, attributes) =>
        {
            evaluatedTargetingKey = targetingKey;
            return new TestEvaluation(new Dictionary<string, string> { ["__dd_allocation_key"] = "alloc", [FeatureFlagMetadataKeys.DoLog] = "true" });
        });
        await Api.Instance.SetProviderAsync(domain, provider);
        var client = Api.Instance.GetClient(domain);
        var received = new List<ExposureEvaluation>();
        client.AddHooks(new ExposureHook(e => received.Add(e)));

        await client.GetBooleanDetailsAsync("flag", false, EvaluationContext.Builder().SetTargetingKey("original").Build(), new FlagEvaluationOptions(new ReplaceTargetingKeyHook("evaluated")));

        evaluatedTargetingKey.Should().Be("evaluated");
        received.Should().ContainSingle().Which.TargetingKey.Should().Be("evaluated", "the exposure belongs to the subject the provider evaluated");
        received[0].Context!.TargetingKey.Should().Be("evaluated");
    }

    [Fact]
    public async Task IsExposureRequiresASuccessfulEvaluationOfALoggedAllocation()
    {
        foreach (var (error, variant, allocation, doLog, expected) in new[]
        {
            (null, "on", "alloc", "true", true),
            (null, "on", "alloc", "false", false),
            (null, "on", "alloc", null, false),
            (null, null, "alloc", "true", false),
            (null, "on", null, "true", false),
            ("GENERAL", "on", "alloc", "true", false),
        })
        {
            var evaluation = await EvaluateWithMetadata(error, variant, allocation, doLog, cacheHit: null);

            evaluation.IsExposure.Should().Be(expected, because: $"error={error ?? "none"} variant={variant} allocation={allocation} doLog={doLog}");
        }
    }

    [Theory]
    [InlineData("true", true, false)]
    [InlineData("false", false, true)]
    [InlineData(null, false, false)]
    public async Task CacheVerdictMapsToIsCacheHitAndShouldSend(string? cacheHit, bool expectedCacheHit, bool expectedShouldSend)
    {
        var evaluation = await EvaluateWithMetadata(error: null, variant: "on", allocation: "alloc", doLog: "true", cacheHit: cacheHit);

        evaluation.IsExposure.Should().BeTrue();
        evaluation.IsCacheHit.Should().Be(expectedCacheHit);
        evaluation.ShouldSend.Should().Be(expectedShouldSend);
    }

    [Fact]
    public async Task FeaturesCarryHookDestinationFeaturesWithTheirTypes()
    {
        var metadata = new Dictionary<string, string>
        {
            ["__dd_allocation_key"] = "alloc",
            [FeatureFlagMetadataKeys.DoLog] = "true",
            [FeatureFlagMetadataKeys.ExposureCacheHit] = "false",
            ["__dd_feature.holdout.key"] = "06ad",
            ["__dd_feature.holdout.assignment_group"] = "status_quo",
            ["__dd_feature.holdout.should_include_in_holdout_analysis"] = "false",
            ["__dd_feature.internal.count"] = "3",
            ["__dd_exposure_feature.holdout.key"] = "06ad",
            ["__dd_evaluation_feature.holdout.key"] = "06ad",
            [FeatureFlagMetadataKeys.FeatureTypes] = "42:holdout.should_include_in_holdout_analysisb14:internal.countn",
        };
        var received = new List<ExposureEvaluation>();
        var hook = new ExposureHook(e => received.Add(e));
        var evaluation = new TestEvaluation(metadata);

        await hook.FinallyAsync(BoolContext(), Details(evaluation));
        var features = received.Single().Features;

        features.Should().HaveCount(4, "only HOOK-destination features are exposed");
        features["holdout.key"].Should().Be("06ad");
        features["holdout.assignment_group"].Should().Be("status_quo");
        features["holdout.should_include_in_holdout_analysis"].Should().Be(false);
        features["internal.count"].Should().Be(3d);
    }

    [Fact]
    public async Task FeaturesAreEmptyWithoutFeatureMetadata()
    {
        var received = new List<ExposureEvaluation>();
        var hook = new ExposureHook(e => received.Add(e));
        var evaluation = new TestEvaluation(new Dictionary<string, string> { ["__dd_allocation_key"] = "alloc" });

        await hook.FinallyAsync(BoolContext(), Details(evaluation));

        received.Single().Features.Should().BeEmpty();
        received.Single().IsExposure.Should().BeFalse("an old tracer emits no do-log metadata outside span enrichment");
    }

    [Fact]
    public async Task ACustomerExceptionDoesNotAffectTheEvaluationOrLaterCallbacks()
    {
        var invocations = 0;
        using var logger = new RecordingLogger();
        var hook = new ExposureHook(
            _ =>
            {
                invocations++;
                if (invocations <= 2)
                {
                    throw new InvalidOperationException("customer code failed");
                }
            },
            logger);

        var first = await Evaluate(hook, error: null);
        var second = await Evaluate(hook, error: null);
        var third = await Evaluate(hook, error: null);

        first.Should().BeTrue();
        second.Should().BeTrue();
        third.Should().BeTrue();
        invocations.Should().Be(3, "the callback keeps running after it threw");
        logger.Entries.Should().Equal(
            ("Warning", "Exposure callback failed; later failures are logged at debug level"),
            ("Debug", "Exposure callback failed"));
    }

    [Fact]
    public void ANullCallbackIsRejected()
    {
        var action = () => new ExposureHook(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    private static async Task<bool> Evaluate(ExposureHook hook, string? error)
    {
        var evaluation = error is null
            ? new TestEvaluation(ExposureMetadata(cacheHit: "false"))
            : new TestEvaluation(new Dictionary<string, string>()) { Error = error };
        await hook.FinallyAsync(BoolContext(), Details(evaluation));
        return true;
    }

    private static async Task<ExposureEvaluation> EvaluateWithMetadata(string? error, string? variant, string? allocation, string? doLog, string? cacheHit)
    {
        var received = new List<ExposureEvaluation>();
        var hook = new ExposureHook(e => received.Add(e));
        var metadata = new Dictionary<string, string>();
        if (allocation is not null)
        {
            metadata["__dd_allocation_key"] = allocation;
        }

        if (doLog is not null)
        {
            metadata[FeatureFlagMetadataKeys.DoLog] = doLog;
        }

        if (cacheHit is not null)
        {
            metadata[FeatureFlagMetadataKeys.ExposureCacheHit] = cacheHit;
        }

        var evaluation = new TestEvaluation(metadata) { Variant = variant, Error = error };
        await hook.FinallyAsync(BoolContext(), Details(evaluation));
        return received.Single();
    }

    private static Dictionary<string, string> ExposureMetadata(string cacheHit)
        => new()
        {
            ["__dd_allocation_key"] = "alloc",
            [FeatureFlagMetadataKeys.DoLog] = "true",
            [FeatureFlagMetadataKeys.ExposureCacheHit] = cacheHit,
        };

    private static HookContext<bool> BoolContext()
        => new("flag", false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), EvaluationContext.Builder().SetTargetingKey("user-123").Build());

    private static FlagEvaluationDetails<bool> Details(IEvaluation evaluation)
    {
        var resolution = FeatureFlagsSdk.GetResolutionDetails("flag", false, evaluation, null);
        return new FlagEvaluationDetails<bool>(resolution.FlagKey, resolution.Value, resolution.ErrorType, resolution.Reason, resolution.Variant, resolution.ErrorMessage, resolution.FlagMetadata);
    }

    internal sealed class ReplaceTargetingKeyHook(string targetingKey) : Hook
    {
        public override ValueTask<EvaluationContext> BeforeAsync<T>(HookContext<T> context, IReadOnlyDictionary<string, object>? hints = null, System.Threading.CancellationToken cancellationToken = default)
            => new(EvaluationContext.Builder().SetTargetingKey(targetingKey).Build());
    }

    internal sealed class TestEvaluation(Dictionary<string, string> metadata) : IEvaluation
    {
        public string? Variant { get; set; } = "on";

        public string FlagKey => "flag";

        public object Value => true;

        public EvaluationReason Reason => Error is null ? EvaluationReason.Static : EvaluationReason.Error;

        public string? Error { get; set; }

        public IDictionary<string, string>? FlagMetadata => metadata;
    }

    internal sealed class RecordingLogger : ILogger, IDisposable
    {
        public List<(string Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
            {
                Entries.Add((logLevel.ToString(), formatter(state, exception)));
            }
        }

        public void Dispose()
        {
        }
    }
}
