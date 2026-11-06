// <copyright file="DeferredExposureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

using ExposureEvent = Datadog.Trace.FeatureFlags.Exposure.Model.ExposureEvent;
using FeatureFlagsValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class DeferredExposureTests
{
    [Fact]
    public void ADeferredEvaluationWithoutAPeekLeavesTheExposurePendingAndStampsNothing()
    {
        var sent = new List<ExposureEvent>();
        var evaluator = new FeatureFlagsEvaluator(Record(sent), new ServerConfiguration { Flags = Flags(doLog: true) });

        var deferred = evaluator.Evaluate("flag", FeatureFlagsValueType.String, "default", new EvaluationContext("user"), deferExposure: true);
        var immediate = evaluator.Evaluate("flag", FeatureFlagsValueType.String, "default", new EvaluationContext("user"));

        var pendingMetadata = deferred.FlagMetadata.Should().BeOfType<DeferredExposureMetadata>().Subject;
        pendingMetadata.Keys.Should().NotContain(FeatureFlagMetadataKeys.ExposureCacheHit);
        pendingMetadata.TryTakePendingExposure(out var pending).Should().BeTrue();
        pending.Subject.Id.Should().Be("user");
        immediate.FlagMetadata.Should().NotBeOfType<DeferredExposureMetadata>();
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
        (result.FlagMetadata is DeferredExposureMetadata { IsPending: true }).Should().BeFalse();
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

    [Fact]
    public void ADeferredEvaluationCarriesTheCacheAnswerWithoutRecordingIt()
    {
        using var module = Module();

        var first = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        var uncommitted = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        first.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false");
        uncommitted.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false", "the advisory answer records nothing");
        module.CommitExposure(uncommitted.FlagMetadata).Should().Be(ExposureCommitResult.New);

        var repeat = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        repeat.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("true");
        module.CommitExposure(repeat.FlagMetadata).Should().Be(ExposureCommitResult.Repeat);
    }

    [Fact]
    public void TheAdvisoryAnswerRecordsNothingOnceExposuresAreRecorded()
    {
        using var module = Module();
        // Recording another subject first creates the exposure API, so the peeks below reach the cache.
        var other = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "other", null, deferExposure: true);
        module.CommitExposure(other.FlagMetadata).Should().Be(ExposureCommitResult.New);

        var uncommitted = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        var later = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        uncommitted.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false");
        later.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false", "the first peek recorded nothing");
        module.CommitExposure(later.FlagMetadata).Should().Be(ExposureCommitResult.New, "an uncommitted evaluation must not consume the subject's first exposure");
    }

    [Fact]
    public void TheAdvisoryAnswerDoesNotCreateTheExposureApi()
    {
        using var module = Module();

        var evaluation = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        evaluation.FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("false");
        typeof(FeatureFlagsModule).GetField("_exposureApi", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(module)
                                  .Should().BeNull("only a commit or an immediate report creates the exposure API and its transport");
    }

    [Fact]
    public async Task NoCacheAnswerIsStampedOnceExposureReportingHasStopped()
    {
        var module = Module();
        await module.DisposeAsync();

        var evaluation = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        var metadata = evaluation.FlagMetadata.Should().BeOfType<DeferredExposureMetadata>().Subject;
        metadata.IsPending.Should().BeTrue();
        metadata.Keys.Should().NotContain(FeatureFlagMetadataKeys.ExposureCacheHit);
    }

    [Fact]
    public void ACommitAfterTheModuleWasReplacedStillRecordsTheExposure()
    {
        using var original = Module();
        using var replacement = Module();

        var evaluation = original.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        // Tracer.Configure can replace the module between the evaluation and the provider's commit.
        replacement.CommitExposure(evaluation.FlagMetadata).Should().Be(ExposureCommitResult.New);
        original.CommitExposure(evaluation.FlagMetadata).Should().Be(ExposureCommitResult.NothingPending, "an evaluation commits at most once");
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void TheUncommittedExposureWarningIsLoggedOnceAndOnlyWithoutCommits(bool commit, int expectedWarnings)
    {
        var logger = new Mock<IDatadogLogger>();
        using var module = Module(logger.Object);

        for (var i = 0; i < 2_500; i++)
        {
            var evaluation = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
            if (commit)
            {
                module.CommitExposure(evaluation.FlagMetadata);
            }
        }

        UncommittedWarnings(logger).Should().Be(expectedWarnings);
    }

    [Fact]
    public void AnyCommitCallRestartsTheCountAndEachModuleWarnsOnce()
    {
        var logger = new Mock<IDatadogLogger>();
        using var module = Module(logger.Object);

        Defer(module, 999);
        module.CommitExposure(null).Should().Be(ExposureCommitResult.NothingPending);
        Defer(module, 999);
        UncommittedWarnings(logger).Should().Be(0, "a commit call, even one that finds nothing pending, shows the provider's hooks run");

        Defer(module, 1);
        UncommittedWarnings(logger).Should().Be(1, "the 1,000th uncommitted exposure since the last commit warns");

        module.CommitExposure(null);
        Defer(module, 1_000);
        UncommittedWarnings(logger).Should().Be(1, "the warning is written once per module");

        var replacementLogger = new Mock<IDatadogLogger>();
        using var replacement = Module(replacementLogger.Object);
        Defer(replacement, 1_000);
        UncommittedWarnings(replacementLogger).Should().Be(1, "a module that replaces it after Tracer.Configure warns again");
    }

    private static void Defer(FeatureFlagsModule module, int count)
    {
        for (var i = 0; i < count; i++)
        {
            module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        }
    }

    private static int UncommittedWarnings(Mock<IDatadogLogger> logger)
        => logger.Invocations.Count(i => i.Method.Name == nameof(IDatadogLogger.Warning)
                                      && i.Arguments.OfType<string>().Any(a => a.Contains("OpenFeature hooks did not commit")));

    private static FeatureFlagsModule Module(IDatadogLogger? logger = null)
    {
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config" },
            { "DD_FEATURE_FLAGS_EXPOSURES_DATADOG_LOGGING_ENABLED", "false" },
        }));
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), logger: logger)!;
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
