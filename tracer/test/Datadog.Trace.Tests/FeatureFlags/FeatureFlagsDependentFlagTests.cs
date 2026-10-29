// <copyright file="FeatureFlagsDependentFlagTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using ExposureEvent = Datadog.Trace.FeatureFlags.Exposure.Model.ExposureEvent;
using ValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagsDependentFlagTests
{
    [Fact]
    public void EvaluatesDependencyAndCommitsOrderedExposureProvenance()
    {
        var config = Configuration(
            StaticFlag("prerequisite", "on"),
            DependentFlag(
                "root",
                Allocation("rejected", [Dependency("prerequisite", "on"), Attribute("plan", "paid")], "selected"),
                Allocation("selected", [Dependency("prerequisite", "on")], "selected")));
        var exposures = new List<ExposureEvent>();
        var dependencies = new List<Evaluation>();
        var dependencyEvaluationTime = default(DateTime);
        var evaluator = new FeatureFlagsEvaluator(
            (in ExposureEvent exposure) => exposures.Add(exposure),
            config,
            onDependencyEvaluation: (evaluation, evaluationTime, _) =>
            {
                dependencies.Add(evaluation);
                dependencyEvaluationTime = evaluationTime;
            });

        var result = evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject", new Dictionary<string, object?> { ["plan"] = "free" }));

        var expectedTimestamp = new DateTimeOffset(dependencyEvaluationTime).ToUnixTimeMilliseconds();
        result.Value.Should().Be("selected");
        result.FlagMetadata![FeatureFlagMetadataKeys.EvaluationTimestampMs].Should().Be(expectedTimestamp.ToString(CultureInfo.InvariantCulture));
        dependencies.Select(e => e.FlagKey).Should().Equal("prerequisite");
        exposures.Select(e => e.Flag.Key).Should().Equal("prerequisite", "root");
        exposures.Select(e => e.Timestamp).Should().OnlyContain(timestamp => timestamp == expectedTimestamp);
    }

    [Fact]
    public void RejectedDependencyKeepsTelemetryButRollsBackItsExposure()
    {
        var root = DependentFlag(
            "root",
            Allocation("dependency", [Dependency("prerequisite", "required")], "selected"),
            Allocation("fallback", null, "selected"));
        var exposures = new List<ExposureEvent>();
        var dependencies = new List<Evaluation>();
        var evaluator = Evaluator(Configuration(StaticFlag("prerequisite", "other"), root), exposures, dependencies);

        evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        dependencies.Should().ContainSingle().Which.Variant.Should().Be("other");
        exposures.Select(e => e.Flag.Key).Should().Equal("root");
    }

    [Fact]
    public void LaterConditionRollbackDoesNotLeakDependencyExposure()
    {
        var root = DependentFlag(
            "root",
            Allocation("rejected", [Dependency("prerequisite", "on"), Attribute("plan", "paid")], "selected"),
            Allocation("fallback", null, "selected"));
        var exposures = new List<ExposureEvent>();
        var dependencies = new List<Evaluation>();
        var evaluator = Evaluator(Configuration(StaticFlag("prerequisite", "on"), root), exposures, dependencies);

        evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject", new Dictionary<string, object?> { ["plan"] = "free" }));

        dependencies.Should().ContainSingle();
        exposures.Select(e => e.Flag.Key).Should().Equal("root");
    }

    [Fact]
    public void ConditionsShortCircuitBeforeUnreachedDependency()
    {
        var root = DependentFlag(
            "root",
            Allocation("rejected", [Attribute("plan", "paid"), Dependency("prerequisite", "on")], "selected"),
            Allocation("fallback", null, "selected"));
        var dependencies = new List<Evaluation>();
        var evaluator = Evaluator(Configuration(StaticFlag("prerequisite", "on"), root), dependencies: dependencies);

        evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject", new Dictionary<string, object?> { ["plan"] = "free" }));

        dependencies.Should().BeEmpty();
    }

    [Fact]
    public void NoAssignmentRollsBackBeforeLaterAllocationAndMemoReplay()
    {
        var noAssignment = Allocation("no-assignment", [Dependency("prerequisite", "on")], "selected");
        noAssignment.Splits![0].Shards = [new Shard { Salt = "none", TotalShards = 1, Ranges = [] }];
        var root = DependentFlag(
            "root",
            noAssignment,
            Allocation("selected", [Dependency("prerequisite", "on")], "selected"));
        var exposures = new List<ExposureEvent>();
        var dependencies = new List<Evaluation>();
        var evaluator = Evaluator(Configuration(StaticFlag("prerequisite", "on"), root), exposures, dependencies);

        evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        dependencies.Should().ContainSingle("memoized dependencies emit evaluation telemetry once");
        exposures.Select(e => e.Flag.Key).Should().Equal("prerequisite", "root");
    }

    [Fact]
    public void DependencyFailurePropagatesAndRollsBackRootTransaction()
    {
        var exposures = new List<ExposureEvent>();
        var dependencies = new List<Evaluation>();
        var evaluator = Evaluator(
            Configuration(DependentFlag("root", Allocation("root", [Dependency("missing", "on")], "selected"))),
            exposures,
            dependencies);

        var result = evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        result.Reason.Should().Be(EvaluationReason.Error);
        result.Error.Should().Be("FLAG_NOT_FOUND");
        dependencies.Should().ContainSingle().Which.FlagMetadata!["errorCode"].Should().Be("FLAG_NOT_FOUND");
        exposures.Should().BeEmpty();
    }

    [Fact]
    public void DetectsCyclesAndInclusiveMaximumDepth()
    {
        var cycle = Configuration(
            DependentFlag("a", Allocation("a", [Dependency("b", "on")], "on")),
            DependentFlag("b", Allocation("b", [Dependency("a", "on")], "on")));
        var cycleResult = Evaluator(cycle).Evaluate("a", ValueType.String, "fallback", new EvaluationContext("subject"));

        cycleResult.Reason.Should().Be(EvaluationReason.Error);
        cycleResult.Error.Should().Be("GENERAL");

        var depth = Configuration(
            DependentFlag("root", Allocation("root", [Dependency("middle", "on")], "on")),
            DependentFlag("middle", Allocation("middle", [Dependency("leaf", "on")], "on")),
            StaticFlag("leaf", "on"));
        depth.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = 1 };
        var dependencies = new List<Evaluation>();
        Evaluator(depth, dependencies: dependencies).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Reason.Should().Be(EvaluationReason.Error);
        dependencies.Select(e => e.FlagKey).Should().Equal("leaf", "middle");
        dependencies.Should().OnlyContain(e => e.FlagMetadata!["errorCode"] == "GENERAL");

        depth.EvaluatorParams.MaxDependencyDepth = 2;
        Evaluator(depth).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, true)]
    [InlineData(255, false)]
    [InlineData(-1, true)]
    [InlineData(256, true)]
    [InlineData(1.5, true)]
    public void EvaluatorParamsAreOptionalAndValidated(object? maxDepth, bool expectError)
    {
        var config = Configuration(
            StaticFlag("dependency", "on"),
            DependentFlag("root", Allocation("root", [Dependency("dependency", "on")], "on")));
        if (maxDepth is not null)
        {
            config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = maxDepth };
        }

        var result = Evaluator(config).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        (result.Reason == EvaluationReason.Error).Should().Be(expectError);
        if (expectError && maxDepth is not 0)
        {
            result.Error.Should().Be("PARSE_ERROR");
        }
    }

    [Fact]
    public void UnusedInvalidMaximumDepthDoesNotAffectOrdinaryFlag()
    {
        var config = Configuration(StaticFlag("root", "on"));
        config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = "invalid" };

        var result = Evaluator(config).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        result.Value.Should().Be("on");
        result.Reason.Should().NotBe(EvaluationReason.Error);
    }

    [Fact]
    public void ExplicitNullMaximumDepthIsIgnoredWhenUnusedAndRejectedWhenUsed()
    {
        var config = Configuration(
            StaticFlag("ordinary", "on"),
            StaticFlag("dependency", "on"),
            DependentFlag("dependent", Allocation("dependent", [Dependency("dependency", "on")], "on")));
        config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = null };

        Evaluator(config).Evaluate("ordinary", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");
        var dependent = Evaluator(config).Evaluate("dependent", ValueType.String, "fallback", new EvaluationContext("subject"));

        dependent.Reason.Should().Be(EvaluationReason.Error);
        dependent.Error.Should().Be("PARSE_ERROR");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MemoizedSubtreeDepthFailureEmitsEachActuallyEvaluatedDependencyOnce(bool sharedSubtreeFirst)
    {
        var rootConditions = sharedSubtreeFirst
                                 ? new List<ConditionConfiguration> { Dependency("a", "on"), Dependency("b", "on") }
                                 : new List<ConditionConfiguration> { Dependency("b", "on"), Dependency("a", "on") };
        var config = Configuration(
            StaticFlag("leaf", "on"),
            DependentFlag("a", Allocation("a", [Dependency("leaf", "on")], "on")),
            DependentFlag("b", Allocation("b", [Dependency("a", "on")], "on")),
            DependentFlag("root", Allocation("root", rootConditions, "on")));
        config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = 2 };
        var dependencies = new List<Evaluation>();

        var result = Evaluator(config, dependencies: dependencies).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        result.Reason.Should().Be(EvaluationReason.Error);
        dependencies.Select(e => e.FlagKey).Should().Equal("leaf", "a", "b");
        dependencies.Select(e => e.FlagKey).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("ONE_OF")]
    [InlineData("NOT_ONE_OF")]
    public void DependencyConditionMembersMustAllBeStrings(string operatorName)
    {
        var condition = Dependency("dependency", "on");
        condition.Operator = operatorName == "ONE_OF" ? ConditionOperator.ONE_OF : ConditionOperator.NOT_ONE_OF;
        condition.Value = new JArray("on", 1);
        var config = Configuration(
            StaticFlag("dependency", "on"),
            DependentFlag("root", Allocation("root", [condition], "on")));

        var result = Evaluator(config).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        result.Error.Should().Be("PARSE_ERROR");
    }

    [Fact]
    public void ConditionSourceMustBeExactlyOneOfAttributeOrFlagEvaluation()
    {
        var condition = Dependency("dependency", "on");
        condition.Attribute = "plan";
        var config = Configuration(
            StaticFlag("dependency", "on"),
            DependentFlag("root", Allocation("root", [condition], "on")));

        var result = Evaluator(config).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        result.Error.Should().Be("PARSE_ERROR");
    }

    private static FeatureFlagsEvaluator Evaluator(ServerConfiguration config, List<ExposureEvent>? exposures = null, List<Evaluation>? dependencies = null)
        => new(
            exposures is null ? null : (in ExposureEvent exposure) => exposures.Add(exposure),
            config,
            onDependencyEvaluation: dependencies is null ? null : (evaluation, _, _) => dependencies.Add(evaluation));

    private static ServerConfiguration Configuration(params Flag[] flags)
    {
        var collection = new FlagCollection();
        foreach (var flag in flags)
        {
            collection.Add(flag.Key!, flag);
        }

        return new ServerConfiguration { Flags = collection };
    }

    private static Flag StaticFlag(string key, string variant)
        => DependentFlag(key, Allocation(key + "-allocation", null, variant));

    private static Flag DependentFlag(string key, params Allocation[] allocations)
        => new()
        {
            Key = key,
            Enabled = true,
            VariationType = ValueType.String,
            Variations = allocations.SelectMany(a => a.Splits!).Select(s => s.VariationKey!).Distinct().ToDictionary(v => v, v => new Variant(v, v)),
            Allocations = [.. allocations],
        };

    private static Allocation Allocation(string key, List<ConditionConfiguration>? conditions, string variant)
        => new()
        {
            Key = key,
            DoLog = true,
            Rules = conditions is null ? null : [new Rule(conditions)],
            Splits = [new Split { VariationKey = variant, Shards = [] }],
        };

    private static ConditionConfiguration Dependency(string key, string variant)
        => new() { FlagEvaluation = new FlagEvaluationConfiguration { Key = key }, Operator = ConditionOperator.ONE_OF, Value = new[] { variant } };

    private static ConditionConfiguration Attribute(string key, string value)
        => new() { Attribute = key, Operator = ConditionOperator.ONE_OF, Value = new[] { value } };
}
