// <copyright file="DeferredExposureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

using ExposureEvent = Datadog.Trace.FeatureFlags.Exposure.Model.ExposureEvent;
using FeatureFlagsValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class DeferredExposureTests
{
    [Fact]
    public void ADeferredEvaluationLeavesTheExposurePendingAndStampsNoVerdict()
    {
        var sent = new List<ExposureEvent>();
        var evaluator = new FeatureFlagsEvaluator(Record(sent), new ServerConfiguration { Flags = Flags(doLog: true) });

        var deferred = evaluator.Evaluate("flag", FeatureFlagsValueType.String, "default", new EvaluationContext("user"), deferExposure: true);
        var immediate = evaluator.Evaluate("flag", FeatureFlagsValueType.String, "default", new EvaluationContext("user"));

        deferred.PendingExposure.Should().NotBeNull();
        deferred.PendingExposure!.Value.Subject.Id.Should().Be("user");
        deferred.FlagMetadata!.Keys.Should().NotContain(FeatureFlagMetadataKeys.ExposureCacheHit);
        immediate.PendingExposure.Should().BeNull();
        immediate.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false");
        sent.Should().ContainSingle("only the immediate evaluation sends during evaluation");
    }

    [Theory]
    [InlineData("missing-flag", FeatureFlagsValueType.String)]
    [InlineData("flag", FeatureFlagsValueType.Boolean)]
    public void AFailedDeferredEvaluationLeavesNothingPending(string flagKey, FeatureFlagsValueType type)
    {
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = Flags(doLog: true) });

        var result = evaluator.Evaluate(flagKey, type, "default", new EvaluationContext("user"), deferExposure: true);

        result.Error.Should().NotBeNull();
        result.PendingExposure.Should().BeNull();
    }

    [Fact]
    public void CommitRecordsOnceAndReturnsTheVerdict()
    {
        using var module = Module();

        var first = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        var second = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        module.CommitExposure(first.FlagMetadata).Should().Be(ExposureCommitResult.New);
        module.CommitExposure(first.FlagMetadata).Should().Be(ExposureCommitResult.NothingPending, "an evaluation commits at most once");
        module.CommitExposure(second.FlagMetadata).Should().Be(ExposureCommitResult.Repeat);
        module.CommitExposure(null).Should().Be(ExposureCommitResult.NothingPending);
    }

    [Fact]
    public void CommitFindsTheExposureOnlyThroughTheReturnedMetadataInstance()
    {
        using var module = Module();

        var evaluation = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        module.CommitExposure(new Dictionary<string, string>(evaluation.FlagMetadata!)).Should().Be(ExposureCommitResult.NothingPending);
        module.CommitExposure(evaluation.FlagMetadata).Should().Be(ExposureCommitResult.New);
    }

    [Fact]
    public void UncommittedEvaluationsRecordNothing()
    {
        using var module = Module();

        module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        var later = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        module.CommitExposure(later.FlagMetadata).Should().Be(ExposureCommitResult.New, "the first evaluation was never committed, as after a failed hook");
    }

    [Fact]
    public async Task CommitsAfterShutdownGiveNoVerdict()
    {
        var module = Module();
        var first = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        var second = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        await module.DisposeAsync();

        module.CommitExposure(first.FlagMetadata).Should().Be(ExposureCommitResult.NothingPending, "nothing can be recorded once exposure reporting has stopped");
        module.CommitExposure(second.FlagMetadata).Should().Be(ExposureCommitResult.NothingPending);
    }

    [Fact]
    public void ImmediateAndDeferredEvaluationsShareOneCache()
    {
        using var module = Module();

        var immediate = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null);
        var deferred = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        immediate.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false");
        module.CommitExposure(deferred.FlagMetadata).Should().Be(ExposureCommitResult.Repeat);
    }

    [Fact]
    public async Task ConcurrentFirstEvaluationsOfOneSubjectGiveExactlyOneNewExposure()
    {
        using var module = Module();
        var evaluations = Enumerable.Range(0, 64)
                                    .Select(_ => module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true))
                                    .ToList();

        var verdicts = await Task.WhenAll(evaluations.Select(e => Task.Run(() => module.CommitExposure(e.FlagMetadata))));

        verdicts.Count(v => v == ExposureCommitResult.New).Should().Be(1);
        verdicts.Count(v => v == ExposureCommitResult.Repeat).Should().Be(63);
    }

    private static FeatureFlagsModule Module()
    {
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config" },
            { "DD_FEATURE_FLAGS_EXPOSURES_DATADOG_LOGGING_ENABLED", "false" },
        }));
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager())!;
        module.ApplyConfiguration(new ServerConfiguration { Flags = Flags(doLog: true) }).Should().BeTrue();
        return module;
    }

    private static FeatureFlagsEvaluator.ReportExposureDelegate Record(List<ExposureEvent> sent)
        => (in ExposureEvent e) =>
        {
            sent.Add(e);
            return ExposureRecordResult.New;
        };

    private static FlagCollection Flags(bool doLog)
    {
        var variants = new Dictionary<string, Variant> { ["on"] = new Variant("on", "value") };
        var split = new Split { VariationKey = "on", Shards = new List<Shard>(), SerialId = 7 };
        var allocation = new Allocation { Key = "allocation", Splits = new List<Split> { split }, DoLog = doLog };
        var flag = new Flag { Key = "flag", Enabled = true, VariationType = FeatureFlagsValueType.String, Variations = variants, Allocations = new List<Allocation> { allocation } };
        return new FlagCollection { ["flag"] = flag };
    }
}
