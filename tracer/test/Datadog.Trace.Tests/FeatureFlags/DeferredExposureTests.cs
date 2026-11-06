// <copyright file="DeferredExposureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

using FeatureFlagsValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class DeferredExposureTests
{
    [Fact]
    public void CommitRecordsOnceAndReturnsTheVerdict()
    {
        using var module = Module();

        var first = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);
        var second = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);

        module.CommitExposure(first.FlagMetadata).Should().Be(ExposureCommitResult.New);
        module.CommitExposure(first.FlagMetadata).Should().Be(ExposureCommitResult.NothingPending, "an evaluation commits at most once");
        module.CommitExposure(second.FlagMetadata).Should().Be(ExposureCommitResult.Repeat);
        module.CommitExposure(null).Should().Be(ExposureCommitResult.NothingPending);
    }

    [Fact]
    public void CommitFindsTheExposureOnlyThroughTheReturnedMetadataInstance()
    {
        using var module = Module();

        var evaluation = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);

        module.CommitExposure(new Dictionary<string, string>(evaluation.FlagMetadata!)).Should().Be(ExposureCommitResult.NothingPending);
        module.CommitExposure(evaluation.FlagMetadata).Should().Be(ExposureCommitResult.New);
    }

    [Fact]
    public void ImmediateDependentEvaluationStampsTheRootExposureVerdict()
    {
        var flags = Flags(doLog: true);
        flags["root"] = DependentRoot(doLog: true);
        using var module = Module(flags);

        var first = module.Evaluate("root", FeatureFlagsValueType.String, "default", "user", null);
        var second = module.Evaluate("root", FeatureFlagsValueType.String, "default", "user", null);

        first.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false");
        second.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("true");
    }

    [Fact]
    public void UncommittedEvaluationsRecordNothing()
    {
        using var module = Module();

        module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);
        var later = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);

        module.CommitExposure(later.FlagMetadata).Should().Be(ExposureCommitResult.New, "the first evaluation was never committed, as after a failed hook");
    }

    [Fact]
    public async Task CommitsAfterShutdownGiveNoVerdict()
    {
        var module = Module();
        var first = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);
        var second = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);

        await module.DisposeAsync();

        module.CommitExposure(first.FlagMetadata).Should().Be(ExposureCommitResult.NothingPending, "nothing can be recorded once exposure reporting has stopped");
        module.CommitExposure(second.FlagMetadata).Should().Be(ExposureCommitResult.NothingPending);
    }

    [Fact]
    public async Task ImmediateEvaluationAfterShutdownStampsNoVerdict()
    {
        var module = Module();
        await module.DisposeAsync();

        var evaluation = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null);

        evaluation.Value.Should().Be("value");
        evaluation.FlagMetadata!.Should().NotContainKey(FeatureFlagMetadataKeys.ExposureCacheHit);
    }

    [Fact]
    public void ImmediateAndDeferredEvaluationsShareOneCache()
    {
        using var module = Module();

        var immediate = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null);
        var deferred = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);

        immediate.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false");
        module.CommitExposure(deferred.FlagMetadata).Should().Be(ExposureCommitResult.Repeat);
    }

    [Fact]
    public async Task ConcurrentFirstEvaluationsOfOneSubjectGiveExactlyOneNewExposure()
    {
        using var module = Module();
        var evaluations = Enumerable.Range(0, 64)
                                    .Select(_ => module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false))
                                    .ToList();

        var verdicts = await Task.WhenAll(evaluations.Select(e => Task.Run(() => module.CommitExposure(e.FlagMetadata))));

        verdicts.Count(v => v == ExposureCommitResult.New).Should().Be(1);
        verdicts.Count(v => v == ExposureCommitResult.Repeat).Should().Be(63);
    }

    [Fact]
    public void DependencyBatchRemainsPendingWhenRootDisablesExposureLogging()
    {
        var flags = Flags(doLog: true);
        var rootAllocation = new Allocation
        {
            Key = "root-allocation",
            DoLog = false,
            Rules =
            [
                new Rule(
                [
                    new ConditionConfiguration
                    {
                        FlagEvaluation = new FlagEvaluationConfiguration { Key = "flag" },
                        Operator = ConditionOperator.ONE_OF,
                        Value = new[] { "on" },
                    },
                ]),
            ],
            Splits = [new Split { VariationKey = "on", Shards = [] }],
        };
        flags["root"] = new Flag
        {
            Key = "root",
            Enabled = true,
            VariationType = FeatureFlagsValueType.String,
            Variations = new Dictionary<string, Variant> { ["on"] = new("on", "root-value") },
            Allocations = [rootAllocation],
        };
        using var module = Module(flags);

        var evaluation = module.Evaluate("root", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);

        evaluation.FlagMetadata![FeatureFlagMetadataKeys.PendingExposure].Should().Be("true");
        module.CommitExposure(evaluation.FlagMetadata).Should().Be(
            ExposureCommitResult.NothingPending,
            "the dependency was recorded but the root itself did not authorize an exposure verdict");
        module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null)
              .FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("true");
    }

    [Fact]
    public void CommitContinuesAfterDependencyReporterFailureAndReturnsRootVerdict()
    {
        var flags = Flags(doLog: true);
        flags["root"] = DependentRoot(doLog: true);
        var settings = Settings(exposuresDatadogLoggingEnabled: true);
        using var transport = new FeatureFlagsEvpTransport(settings, NullDiscoveryService.Instance);
        var attempts = 0;
        var exposures = new ExposureApi(
            settings,
            transport,
            TimeSpan.FromHours(1),
            TimeSpan.FromSeconds(5),
            () =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    throw new InvalidOperationException("dependency reporter failed");
                }
            });
        using var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager())!;
        module.SetExposureApiForTesting(exposures);
        module.ApplyConfiguration(new ServerConfiguration { Flags = flags }).Should().BeTrue();

        var evaluation = module.Evaluate("root", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true, reportRootEvaluation: false);

        module.CommitExposure(evaluation.FlagMetadata).Should().Be(ExposureCommitResult.New);
        attempts.Should().Be(2, "the root exposure must still be attempted after its dependency fails");
    }

    private static FeatureFlagsModule Module(FlagCollection? flags = null)
    {
        var settings = Settings(exposuresDatadogLoggingEnabled: false);
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager())!;
        module.ApplyConfiguration(new ServerConfiguration { Flags = flags ?? Flags(doLog: true) }).Should().BeTrue();
        return module;
    }

    private static TracerSettings Settings(bool exposuresDatadogLoggingEnabled)
        => new(new NameValueConfigurationSource(new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config" },
            { "DD_FEATURE_FLAGS_EXPOSURES_DATADOG_LOGGING_ENABLED", exposuresDatadogLoggingEnabled ? "true" : "false" },
        }));

    private static FlagCollection Flags(bool doLog)
    {
        var variants = new Dictionary<string, Variant> { ["on"] = new Variant("on", "value") };
        var split = new Split { VariationKey = "on", Shards = new List<Shard>(), SerialId = 7 };
        var allocation = new Allocation { Key = "allocation", Splits = new List<Split> { split }, DoLog = doLog };
        var flag = new Flag { Key = "flag", Enabled = true, VariationType = FeatureFlagsValueType.String, Variations = variants, Allocations = new List<Allocation> { allocation } };
        return new FlagCollection { ["flag"] = flag };
    }

    private static Flag DependentRoot(bool doLog)
        => new()
        {
            Key = "root",
            Enabled = true,
            VariationType = FeatureFlagsValueType.String,
            Variations = new Dictionary<string, Variant> { ["on"] = new("on", "root-value") },
            Allocations =
            [
                new Allocation
                {
                    Key = "root-allocation",
                    DoLog = doLog,
                    Rules =
                    [
                        new Rule(
                        [
                            new ConditionConfiguration
                            {
                                FlagEvaluation = new FlagEvaluationConfiguration { Key = "flag" },
                                Operator = ConditionOperator.ONE_OF,
                                Value = new[] { "on" },
                            },
                        ]),
                    ],
                    Splits = [new Split { VariationKey = "on", Shards = [] }],
                },
            ],
        };
}
