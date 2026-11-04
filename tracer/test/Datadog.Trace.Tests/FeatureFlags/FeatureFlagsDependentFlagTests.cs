// <copyright file="FeatureFlagsDependentFlagTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
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
        var evaluator = Evaluator(config);

        var trace = evaluator.EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject", new Dictionary<string, object?> { ["plan"] = "free" }));

        trace.Details.Value.Should().Be("selected");
        trace.DependencyEvaluations.Select(e => e.Evaluation.FlagKey).Should().Equal("prerequisite");
        trace.DependencyExposures.Select(e => e.Flag.Key).Should().Equal("prerequisite", "root");
    }

    [Fact]
    public void RejectedDependencyKeepsTelemetryButRollsBackItsExposure()
    {
        var root = DependentFlag(
            "root",
            Allocation("dependency", [Dependency("prerequisite", "required")], "selected"),
            Allocation("fallback", null, "selected"));
        var evaluator = Evaluator(Configuration(StaticFlag("prerequisite", "other"), root));

        var trace = evaluator.EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        trace.DependencyEvaluations.Should().ContainSingle().Which.Evaluation.Variant.Should().Be("other");
        trace.DependencyExposures.Select(e => e.Flag.Key).Should().Equal("root");
    }

    [Fact]
    public void LaterConditionRollbackDoesNotLeakDependencyExposure()
    {
        var root = DependentFlag(
            "root",
            Allocation("rejected", [Dependency("prerequisite", "on"), Attribute("plan", "paid")], "selected"),
            Allocation("fallback", null, "selected"));
        var evaluator = Evaluator(Configuration(StaticFlag("prerequisite", "on"), root));

        var trace = evaluator.EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject", new Dictionary<string, object?> { ["plan"] = "free" }));

        trace.DependencyEvaluations.Should().ContainSingle();
        trace.DependencyExposures.Select(e => e.Flag.Key).Should().Equal("root");
    }

    [Fact]
    public void ConditionsShortCircuitBeforeUnreachedDependency()
    {
        var root = DependentFlag(
            "root",
            Allocation("rejected", [Attribute("plan", "paid"), Dependency("prerequisite", "on")], "selected"),
            Allocation("fallback", null, "selected"));
        var evaluator = Evaluator(Configuration(StaticFlag("prerequisite", "on"), root));

        var trace = evaluator.EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject", new Dictionary<string, object?> { ["plan"] = "free" }));

        trace.DependencyEvaluations.Should().BeEmpty();
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
        var evaluator = Evaluator(Configuration(StaticFlag("prerequisite", "on"), root));

        var trace = evaluator.EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        trace.DependencyEvaluations.Should().ContainSingle("memoized dependencies emit evaluation telemetry once");
        trace.DependencyExposures.Select(e => e.Flag.Key).Should().Equal("prerequisite", "root");
    }

    [Fact]
    public void DependencyFailurePropagatesAndRollsBackRootTransaction()
    {
        var evaluator = Evaluator(
            Configuration(DependentFlag("root", Allocation("root", [Dependency("missing", "on")], "selected"))));

        var trace = evaluator.EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));
        var result = trace.Details;

        result.Reason.Should().Be(EvaluationReason.Error);
        result.Error.Should().Be("FLAG_NOT_FOUND");
        trace.DependencyEvaluations.Should().ContainSingle().Which.Evaluation.FlagMetadata!["errorCode"].Should().Be("FLAG_NOT_FOUND");
        trace.DependencyExposures.Should().BeEmpty();
    }

    [Fact]
    public void NoAssignmentDependencyFailsRootAndPreventsFallback()
    {
        var noAssignment = Allocation("no-assignment", null, "on");
        noAssignment.Splits![0].Shards = [new Shard { Salt = "none", TotalShards = 1, Ranges = [] }];
        var config = Configuration(
            DependentFlag("prerequisite", noAssignment),
            DependentFlag(
                "root",
                Allocation("dependent", [Dependency("prerequisite", "on")], "selected"),
                Allocation("fallback", null, "fallback")));

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "caller-default", new EvaluationContext("subject"));

        trace.Details.Reason.Should().Be(EvaluationReason.Error);
        trace.Details.Value.Should().Be("caller-default");
        trace.DependencyEvaluations.Should().ContainSingle()
             .Which.Evaluation.FlagMetadata!["errorCode"].Should().Be("GENERAL");
        trace.DependencyExposures.Should().BeEmpty();
    }

    [Fact]
    public void ReturnedBatchOwnsTelemetrySnapshot()
    {
        var nested = new Dictionary<string, object?> { ["plan"] = "paid" };
        var attributes = new Dictionary<string, object?> { ["nested"] = nested };
        var config = Configuration(
            StaticFlag("prerequisite", "on"),
            DependentFlag("root", Allocation("root", [Dependency("prerequisite", "on")], "on")));
        config.ObserveFullEvaluationData = true;

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject", attributes));
        nested["plan"] = "free";
        attributes["added"] = true;
        trace.DependencyEvaluations[0].Evaluation.FlagMetadata!["mutated"] = "yes";

        var snapshot = trace.DependencyEvaluations[0].ContextAttributes!;
        ((Dictionary<string, object?>)snapshot["nested"]!)["plan"].Should().Be("paid");
        snapshot.Should().NotContainKey("added");
        config.Flags!.Find("prerequisite", out var prerequisite).Should().Be(FlagLookupResult.Found);
        prerequisite!.Variations!.Values.Should().OnlyContain(variant => variant.Value!.ToString() == "on");
    }

    [Fact]
    public void DependencyTelemetryPreservesSdkOmissionsAndDoesNotCountRootTargetingKey()
    {
        var attributes = Enumerable.Range(0, 256).ToDictionary(i => "field" + i, i => (object?)i);
        attributes["targetingKey"] = "subject";
        var config = Configuration(
            StaticFlag("prerequisite", "on"),
            DependentFlag("root", Allocation("root", [Dependency("prerequisite", "on")], "on")));
        config.ObserveFullEvaluationData = true;

        var context = new EvaluationContext("subject", attributes, ContextOmissionReason.MaxListElements);
        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", context);
        var dependency = trace.DependencyEvaluations.Should().ContainSingle().Subject;

        dependency.ContextAttributes.Should().HaveCount(256).And.NotContainKey("targetingKey");
        dependency.ContextOmissions.Should().Be(ContextOmissionReason.MaxListElements);
    }

    [Fact]
    public void ReturnedBatchBoundsJsonTokenContextSnapshots()
    {
        var wide = new JArray(Enumerable.Range(0, 2_000).Select(value => new JValue(value)));
        var current = new JObject { ["value"] = true };
        for (var depth = 0; depth < 40; depth++)
        {
            current = new JObject { ["nested"] = current };
        }

        var attributes = new Dictionary<string, object?> { ["deep"] = current, ["wide"] = wide };
        var config = Configuration(
            StaticFlag("prerequisite", "on"),
            DependentFlag("root", Allocation("root", [Dependency("prerequisite", "on")], "on")));
        config.ObserveFullEvaluationData = true;

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject", attributes));
        var snapshot = trace.DependencyEvaluations[0].ContextAttributes!;

        ((object?[])snapshot["wide"]!).Should().HaveCountLessOrEqualTo(1_279);
        object? nested = snapshot["deep"];
        for (var depth = 0; depth < 32 && nested is Dictionary<string, object?> dictionary; depth++)
        {
            nested = dictionary.GetValueOrDefault("nested");
        }

        nested.Should().BeNull();
    }

    [Fact]
    public void ContextNodeBudgetIncludesChildrenRejectedAtMaximumDepth()
    {
        var terminal = new CountingEnumerable(2_000);
        object? value = terminal;
        for (var depth = 0; depth < 31; depth++)
        {
            value = new object?[] { value };
        }

        DependencyEvaluation.SnapshotContext(
            new EvaluationContext("subject", new Dictionary<string, object?> { ["deep"] = value }),
            includeContext: true);

        terminal.Visited.Should().BeLessOrEqualTo(1_280);
    }

    [Fact]
    public void DependencyContextFlattensNestedFieldsForEvaluationTelemetry()
    {
        var snapshot = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?> { ["country"] = "US" },
            ["tags"] = new object?[] { "paid", false },
        };

        var flattened = DependencyEvaluation.FlattenContext(snapshot);
        var observation = new FlagEvalEvent("dependency", "on", null, "subject", 1, flattened, observeFullEvaluationData: true);
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(observation);
        var payload = FlagEvaluationPayload.Encode(
            aggregator.Drain(),
            new Dictionary<string, string>(),
            flushTimeMs: 1,
            payloadLimitBytes: 1_000_000).Payloads.Single();
        var evaluationContext = JObject.Parse(Encoding.UTF8.GetString(payload))["flagEvaluations"]![0]!["context"]!["evaluation"]!;

        observation.ContextAttrs.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["user.country"] = "US",
            ["tags.0"] = "paid",
            ["tags.1"] = false,
        });
        evaluationContext["user.country"]!.Value<string>().Should().Be("US");
        evaluationContext["tags.0"]!.Value<string>().Should().Be("paid");
        evaluationContext["tags.1"]!.Value<bool>().Should().BeFalse();
    }

    [Fact]
    public void DependencyContextRejectsOversizedPathsBeforeTraversingChildren()
    {
        var oversizedKey = new string('x', 257);
        var snapshot = new Dictionary<string, object?>
        {
            [oversizedKey] = Enumerable.Range(0, 256).Cast<object?>().ToArray(),
            [new string('y', 255)] = new object?[] { "too-long" },
            ["valid"] = "retained",
        };

        DependencyEvaluation.FlattenContext(snapshot).Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["valid"] = "retained",
        });
    }

    [Fact]
    public void DependencyContextSkipsOversizedKeysBeforeSnapshottingValuesAndReportsOmissions()
    {
        var throwing = new ThrowingEnumerable();
        var attributes = new Dictionary<string, object?>
        {
            [new string('x', 257)] = throwing,
            ["valid"] = "retained",
        };

        var snapshot = DependencyEvaluation.SnapshotContext(
            new EvaluationContext("subject", attributes),
            includeContext: true,
            out var snapshotOmissions);
        var flattened = DependencyEvaluation.FlattenContext(snapshot, out var flattenOmissions);

        throwing.Enumerated.Should().BeFalse();
        snapshot.Should().BeEquivalentTo(new Dictionary<string, object?> { ["valid"] = "retained" });
        flattened.Should().BeEquivalentTo(snapshot);
        (snapshotOmissions | flattenOmissions).Should().HaveFlag(ContextOmissionReason.MaxKeyLength);
    }

    [Fact]
    public void DependencyContextReportsSnapshotAndFlatteningLimits()
    {
        var throwingContext = new EvaluationContext(
            "subject",
            new Dictionary<string, object?> { ["throwing"] = new ThrowingEnumerable() });

        DependencyEvaluation.SnapshotContext(throwingContext, includeContext: true, out var snapshotOmissions).Should().BeNull();
        snapshotOmissions.Should().HaveFlag(ContextOmissionReason.SnapshotError);

        var oversized = new Dictionary<string, object?>
        {
            ["items"] = Enumerable.Range(0, 300).Cast<object?>().ToArray(),
        };
        DependencyEvaluation.FlattenContext(oversized, out var flattenOmissions).Should().HaveCount(256);
        flattenOmissions.Should().HaveFlag(ContextOmissionReason.MaxListElements);
    }

    [Fact]
    public void DependencyContextDoesNotInvokeUnsupportedValuesAndReportsThem()
    {
        var arbitrary = new ThrowingToString();
        var arbitraryKey = new ThrowingToString();
        var attributes = new Dictionary<string, object?>
        {
            ["arbitrary"] = arbitrary,
            ["token"] = new JConstructor("unsupported", 1),
            ["dictionary"] = new Hashtable { [arbitraryKey] = "value" },
        };

        var snapshot = DependencyEvaluation.SnapshotContext(
            new EvaluationContext("subject", attributes),
            includeContext: true,
            out var omissions);

        arbitrary.Called.Should().BeFalse();
        arbitraryKey.Called.Should().BeFalse();
        snapshot.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["arbitrary"] = null,
            ["token"] = null,
            ["dictionary"] = new Dictionary<string, object?>(),
        });
        omissions.Should().HaveFlag(ContextOmissionReason.UnsupportedValue);
        omissions.Should().NotHaveFlag(ContextOmissionReason.SnapshotError);
    }

    [Fact]
    public void ConditionSerializationPreservesTheSelectedSourcePresence()
    {
        var attribute = new ConditionConfiguration { Attribute = "plan", Operator = ConditionOperator.ONE_OF, Value = new[] { "paid" } };
        var dependency = Dependency("prerequisite", "on");

        var attributeJson = JObject.Parse(JsonConvert.SerializeObject(attribute));
        var dependencyJson = JObject.Parse(JsonConvert.SerializeObject(dependency));

        attributeJson.Should().ContainKey(nameof(ConditionConfiguration.Attribute));
        attributeJson.Should().NotContainKey(nameof(ConditionConfiguration.FlagEvaluation));
        dependencyJson.Should().ContainKey(nameof(ConditionConfiguration.FlagEvaluation));
        dependencyJson.Should().NotContainKey(nameof(ConditionConfiguration.Attribute));
        JsonConvert.DeserializeObject<ConditionConfiguration>(attributeJson.ToString())!.FlagEvaluationPresent.Should().BeFalse();
        JsonConvert.DeserializeObject<ConditionConfiguration>(dependencyJson.ToString())!.AttributePresent.Should().BeFalse();
    }

    [Fact]
    public void ConditionSerializationRetainsExplicitNullForValidation()
    {
        var condition = new ConditionConfiguration { Attribute = null, Operator = ConditionOperator.ONE_OF, Value = new[] { "paid" } };

        var json = JObject.Parse(JsonConvert.SerializeObject(condition));
        var roundTrip = JsonConvert.DeserializeObject<ConditionConfiguration>(json.ToString())!;

        json.Should().ContainKey(nameof(ConditionConfiguration.Attribute));
        json[nameof(ConditionConfiguration.Attribute)]!.Type.Should().Be(JTokenType.Null);
        roundTrip.AttributePresent.Should().BeTrue();
        roundTrip.FlagEvaluationPresent.Should().BeFalse();
    }

    [Fact]
    public async Task ParallelRootEvaluationsHaveIndependentBatches()
    {
        var config = Configuration(
            StaticFlag("left-parent", "on"),
            StaticFlag("right-parent", "on"),
            DependentFlag("left", Allocation("left", [Dependency("left-parent", "on")], "on")),
            DependentFlag("right", Allocation("right", [Dependency("right-parent", "on")], "on")));
        var evaluator = Evaluator(config);

        var traces = await Task.WhenAll(
                         Task.Run(() => evaluator.EvaluateWithTrace("left", ValueType.String, "fallback", new EvaluationContext("left-subject"))),
                         Task.Run(() => evaluator.EvaluateWithTrace("right", ValueType.String, "fallback", new EvaluationContext("right-subject"))));

        traces[0].DependencyEvaluations.Select(record => record.Evaluation.FlagKey).Should().Equal("left-parent");
        traces[1].DependencyEvaluations.Select(record => record.Evaluation.FlagKey).Should().Equal("right-parent");
        traces[0].DependencyEvaluations[0].TargetingKey.Should().Be("left-subject");
        traces[1].DependencyEvaluations[0].TargetingKey.Should().Be("right-subject");
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
        var trace = Evaluator(depth).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));
        trace.Details.Reason.Should().Be(EvaluationReason.Error);
        trace.DependencyEvaluations.Select(e => e.Evaluation.FlagKey).Should().Equal("leaf", "middle");
        trace.DependencyEvaluations.Should().OnlyContain(e => e.Evaluation.FlagMetadata!["errorCode"] == "GENERAL");

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
        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));
        var result = trace.Details;

        result.Reason.Should().Be(EvaluationReason.Error);
        trace.DependencyEvaluations.Select(e => e.Evaluation.FlagKey).Should().Equal("leaf", "a", "b");
        trace.DependencyEvaluations.Select(e => e.Evaluation.FlagKey).Should().OnlyHaveUniqueItems();
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

    private static FeatureFlagsEvaluator Evaluator(ServerConfiguration config) => new(null, config);

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

    private sealed class CountingEnumerable(int count) : IEnumerable
    {
        public int Visited { get; private set; }

        public IEnumerator GetEnumerator()
        {
            for (var i = 0; i < count; i++)
            {
                Visited++;
                yield return i;
            }
        }
    }

    private sealed class ThrowingEnumerable : IEnumerable
    {
        public bool Enumerated { get; private set; }

        public IEnumerator GetEnumerator()
        {
            Enumerated = true;
            throw new InvalidOperationException("must not enumerate");
        }
    }

    private sealed class ThrowingToString
    {
        public bool Called { get; private set; }

        public override string ToString()
        {
            Called = true;
            throw new InvalidOperationException("must not stringify");
        }
    }
}
