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
    public void ADeferredEvaluationLeavesTheExposurePendingAndStampsNothing()
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
    public void DeferredEvaluationsStampNoCacheAnswerAndTheCommitGivesTheVerdict()
    {
        using var module = Module();

        var uncommitted = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        var first = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        module.CommitExposure(first.FlagMetadata).Should().Be(ExposureCommitResult.New);

        var repeat = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        uncommitted.FlagMetadata!.Keys.Should().NotContain(FeatureFlagMetadataKeys.ExposureCacheHit);
        repeat.FlagMetadata!.Keys.Should().NotContain(FeatureFlagMetadataKeys.ExposureCacheHit);
        module.CommitExposure(repeat.FlagMetadata).Should().Be(ExposureCommitResult.Repeat);
    }

    [Fact]
    public void AnUncommittedEvaluationLeavesTheSubjectsFirstExposureToTheCommit()
    {
        using var module = Module();
        var other = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "other", null, deferExposure: true);
        module.CommitExposure(other.FlagMetadata).Should().Be(ExposureCommitResult.New);

        var uncommitted = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);
        var later = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        module.CommitExposure(later.FlagMetadata).Should().Be(ExposureCommitResult.New, "an uncommitted evaluation must not consume the subject's first exposure");
    }

    [Fact]
    public void ADeferredEvaluationDoesNotCreateTheExposureApi()
    {
        using var module = Module();

        var evaluation = module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        evaluation.FlagMetadata.Should().BeOfType<DeferredExposureMetadata>().Which.IsPending.Should().BeTrue();
        module.HasExposureApi.Should().BeFalse("only a commit or an immediate report creates the exposure API and its transport");
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
    public void AnyCommitCallEndsTheCheck()
    {
        var logger = new Mock<IDatadogLogger>();
        using var module = Module(logger.Object);

        Defer(module, 999);
        module.CommitExposure(null).Should().Be(ExposureCommitResult.NothingPending);
        Defer(module, 2_000);

        UncommittedWarnings(logger).Should().Be(0, "a commit call, even one that finds nothing pending, shows the provider's hooks run");
    }

    [Fact]
    public void EachModuleWarnsOnce()
    {
        var logger = new Mock<IDatadogLogger>();
        using var module = Module(logger.Object);

        Defer(module, 999);
        UncommittedWarnings(logger).Should().Be(0);
        Defer(module, 1);
        UncommittedWarnings(logger).Should().Be(1, "the 1,000th uncommitted exposure warns");

        Defer(module, 1_000);
        module.CommitExposure(null);
        Defer(module, 1_000);
        UncommittedWarnings(logger).Should().Be(1, "the warning is written once per module");

        var replacementLogger = new Mock<IDatadogLogger>();
        using var replacement = Module(replacementLogger.Object);
        Defer(replacement, 1_000);
        UncommittedWarnings(replacementLogger).Should().Be(1, "a module that replaces it after Tracer.Configure warns again");
    }

    [Fact]
    public void DependencyBatchRemainsPendingWhenRootDisablesExposureLogging()
    {
        var flags = Flags(doLog: true);
        flags["root"] = DependentRoot(doLog: false);
        using var module = Module(flags);

        var evaluation = module.Evaluate("root", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        evaluation.FlagMetadata![FeatureFlagMetadataKeys.PendingExposure].Should().Be("true");
        module.CommitExposure(evaluation.FlagMetadata).Should().Be(
            ExposureCommitResult.NothingPending,
            "the dependency was recorded but the root itself did not authorize an exposure verdict");
        module.Evaluate("flag", FeatureFlagsValueType.String, "default", "user", null)
              .FlagMetadata![FeatureFlagMetadataKeys.ExposureCacheHit].Should().Be("true");
    }

    [Fact]
    public void DependencyTelemetryCrossesDeferredBoundaryOnceAndReceivesCommitVerdict()
    {
        var flags = Flags(doLog: true);
        var root = DependentRoot(doLog: true);
        var conditions = root.Allocations![0].Rules![0].Conditions!;
        conditions.Add(conditions[0]);
        flags["root"] = root;
        using var module = Module(flags);

        var evaluation = module.Evaluate("root", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        var metadata = evaluation.FlagMetadata.Should().BeOfType<DeferredExposureMetadata>().Subject;
        var dependency = metadata.DependencyTelemetry.Should().ContainSingle("memoized prerequisites cross the boundary once").Subject;
        dependency["flagKey"].Should().Be("flag");
        dependency["exposureEligible"].Should().Be(true);
        dependency["exposureCommitted"].Should().Be(false);

        module.CommitExposure(metadata).Should().Be(ExposureCommitResult.New);
        dependency["exposureCommitted"].Should().Be(true);
        dependency["exposureCacheHit"].Should().Be(false);
    }

    [Fact]
    public void DependencyExposureVerdictUsesTheAttachedMemoizedRecordIndex()
    {
        var first = new Dictionary<string, object?>
        {
            ["flagKey"] = "dependency",
            ["exposureEligible"] = true,
            ["exposureCommitted"] = false,
        };
        var duplicate = new Dictionary<string, object?>
        {
            ["flagKey"] = "dependency",
            ["exposureEligible"] = true,
            ["exposureCommitted"] = false,
        };
        var telemetry = Enumerable.Range(0, 1_000)
                                  .Select(i => (IDictionary<string, object?>)new Dictionary<string, object?> { ["flagKey"] = $"other-{i}" })
                                  .Prepend(first)
                                  .Append(duplicate)
                                  .ToList();
        var metadata = new DeferredExposureMetadata();
        metadata.SetDependencyTelemetry(telemetry);

        // Changing the lookup key after attachment proves the commit path uses the bounded index
        // rather than rescanning every record for every exposure.
        first["flagKey"] = "changed-after-attachment";
        metadata.RecordDependencyExposureResult("dependency", ExposureRecordResult.New);

        first["exposureCommitted"].Should().Be(true);
        first["exposureCacheHit"].Should().Be(false);
        duplicate["exposureCommitted"].Should().Be(false, "memoized prerequisites attach one canonical record");
    }

    [Fact]
    public void RootOnlyDependentBatchWaitsForProviderCommit()
    {
        var flags = Flags(doLog: true);
        flags["root"] = ShortCircuitRoot();
        using var module = Module(flags);

        var evaluation = module.Evaluate(
            "root",
            FeatureFlagsValueType.String,
            "default",
            "user",
            new Dictionary<string, object?> { ["plan"] = "free" },
            deferExposure: true);

        var metadata = evaluation.FlagMetadata.Should().BeOfType<DeferredExposureMetadata>().Subject;
        metadata.DependencyTelemetry.Should().BeEmpty("the prerequisite was short-circuited");
        metadata.IsPending.Should().BeTrue("a provider After hook must decide whether to commit the root exposure");
        module.HasExposureApi.Should().BeFalse("nothing is published before the provider commits");

        module.CommitExposure(metadata).Should().Be(ExposureCommitResult.New);
    }

    [Fact]
    public void FailedGraphCarriesReachedDependencyTelemetryButNoExposureCandidate()
    {
        var flags = new FlagCollection { ["root"] = DependentRoot(doLog: true) };
        using var module = Module(flags);

        var evaluation = module.Evaluate("root", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        evaluation.Error.Should().Be("GENERAL");
        var metadata = evaluation.FlagMetadata.Should().BeOfType<DeferredExposureMetadata>().Subject;
        metadata.IsPending.Should().BeFalse();
        var dependency = metadata.DependencyTelemetry.Should().ContainSingle().Subject;
        dependency["flagKey"].Should().Be("flag");
        dependency["errorCode"].Should().Be("FLAG_NOT_FOUND");
        dependency.Keys.Should().NotContain("exposureEligible");
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

        var evaluation = module.Evaluate("root", FeatureFlagsValueType.String, "default", "user", null, deferExposure: true);

        module.CommitExposure(evaluation.FlagMetadata).Should().Be(ExposureCommitResult.New);
        attempts.Should().Be(2, "the root exposure must still be attempted after its dependency fails");
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
                                      && i.Arguments.OfType<string>().Any(a => a.Contains("left their exposure uncommitted")));

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

    private static FeatureFlagsModule Module(FlagCollection flags)
    {
        var settings = Settings(exposuresDatadogLoggingEnabled: false);
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager())!;
        module.ApplyConfiguration(new ServerConfiguration { Flags = flags }).Should().BeTrue();
        return module;
    }

    private static TracerSettings Settings(bool exposuresDatadogLoggingEnabled)
        => new(new NameValueConfigurationSource(new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config" },
            { "DD_FEATURE_FLAGS_EXPOSURES_DATADOG_LOGGING_ENABLED", exposuresDatadogLoggingEnabled ? "true" : "false" },
        }));

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
                                Value = new object?[] { "on" },
                            },
                        ]),
                    ],
                    Splits = [new Split { VariationKey = "on", Shards = [] }],
                },
            ],
        };

    private static Flag ShortCircuitRoot()
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
                    Key = "short-circuited",
                    DoLog = true,
                    Rules =
                    [
                        new Rule(
                        [
                            new ConditionConfiguration { Attribute = "plan", Operator = ConditionOperator.ONE_OF, Value = new object?[] { "paid" } },
                            new ConditionConfiguration
                            {
                                FlagEvaluation = new FlagEvaluationConfiguration { Key = "missing" },
                                Operator = ConditionOperator.ONE_OF,
                                Value = new object?[] { "on" },
                            },
                        ]),
                    ],
                    Splits = [new Split { VariationKey = "on", Shards = [] }],
                },
                new Allocation
                {
                    Key = "fallback",
                    DoLog = true,
                    Splits = [new Split { VariationKey = "on", Shards = [] }],
                },
            ],
        };
}
