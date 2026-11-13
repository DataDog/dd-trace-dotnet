// <copyright file="FeatureFlagsDependentFlagTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
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

        trace.DependencyEvaluations.Should().ContainSingle();
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
        result.Error.Should().Be("GENERAL");
        trace.DependencyEvaluations.Should().ContainSingle().Which.Evaluation.FlagMetadata!["errorCode"].Should().Be("FLAG_NOT_FOUND");
        trace.DependencyExposures.Should().BeEmpty();
    }

    [Fact]
    public void MissingTargetingKeyInDirectPrerequisiteKeepsLeafCodeAndFailsRootGenerally()
    {
        var prerequisite = StaticFlag("prerequisite", "on");
        prerequisite.Allocations![0].Splits![0].Shards =
        [
            new Shard { Salt = "requires-targeting-key", TotalShards = 1, Ranges = [new ShardRange { Start = 0, End = 1 }] },
        ];
        var config = Configuration(
            prerequisite,
            DependentFlag("root", Allocation("root", [Dependency("prerequisite", "on")], "selected")));

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "caller-default", new EvaluationContext(null));

        trace.Details.Reason.Should().Be(EvaluationReason.Error);
        trace.Details.Error.Should().Be("GENERAL");
        trace.Details.Value.Should().Be("caller-default");
        var dependency = trace.DependencyEvaluations.Should().ContainSingle().Subject.Evaluation;
        dependency.FlagKey.Should().Be("prerequisite");
        dependency.FlagMetadata!["errorCode"].Should().Be("TARGETING_KEY_MISSING");
        trace.DependencyExposures.Should().BeEmpty();
    }

    [Fact]
    public void NotRecordedDependentRootExposureStampsNoCacheVerdict()
    {
        var config = Configuration(
            StaticFlag("prerequisite", "on"),
            DependentFlag("root", Allocation("root", [Dependency("prerequisite", "on")], "selected")));
        var evaluator = new FeatureFlagsEvaluator(
            (in Datadog.Trace.FeatureFlags.Exposure.Model.ExposureEvent _) => ExposureRecordResult.NotRecorded,
            config);

        var result = evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        result.Value.Should().Be("selected");
        result.FlagMetadata!.Should().NotContainKey(FeatureFlagMetadataKeys.ExposureCacheHit);
    }

    [Fact]
    public void TransitiveDependencyFailureKeepsLeafCodeAndMapsAncestorsToGeneral()
    {
        var config = Configuration(
            DependentFlag("middle", Allocation("middle", [Dependency("missing", "on")], "on")),
            DependentFlag("root", Allocation("root", [Dependency("middle", "on")], "on")));

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        trace.Details.Error.Should().Be("GENERAL");
        trace.DependencyEvaluations.Select(item => item.Evaluation.FlagKey).Should().Equal("middle", "missing");
        trace.DependencyEvaluations.Select(item => item.Evaluation.FlagMetadata!["errorCode"]).Should().Equal("GENERAL", "FLAG_NOT_FOUND");
        trace.DependencyExposures.Should().BeEmpty();
    }

    [Fact]
    public void EarlierSuccessfulSiblingIsRetainedBeforeFailureAndAllExposuresAreDiscarded()
    {
        var config = Configuration(
            StaticFlag("successful", "on"),
            DependentFlag(
                "root",
                Allocation("root", [Dependency("successful", "on"), Dependency("missing", "on")], "on")));

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        trace.Details.Reason.Should().Be(EvaluationReason.Error);
        trace.Details.Error.Should().Be("GENERAL");
        trace.DependencyEvaluations.Select(item => item.Evaluation.FlagKey).Should().Equal("successful", "missing");
        trace.DependencyEvaluations[0].Evaluation.FlagMetadata.Should().NotContainKey("errorCode");
        trace.DependencyEvaluations[1].Evaluation.FlagMetadata!["errorCode"].Should().Be("FLAG_NOT_FOUND");
        trace.DependencyExposures.Should().BeEmpty();
    }

    [Fact]
    public void MalformedPrerequisiteKeepsParseErrorAndMapsRootToGeneral()
    {
        var malformed = Dependency("unused", "on");
        malformed.Attribute = "plan";
        var config = Configuration(
            DependentFlag("malformed", Allocation("malformed", [malformed], "on")),
            DependentFlag("root", Allocation("root", [Dependency("malformed", "on")], "on")));

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        trace.Details.Error.Should().Be("GENERAL");
        trace.DependencyEvaluations.Should().ContainSingle();
        trace.DependencyEvaluations[0].Evaluation.FlagKey.Should().Be("malformed");
        trace.DependencyEvaluations[0].Evaluation.FlagMetadata!["errorCode"].Should().Be("PARSE_ERROR");
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
            nested = dictionary.TryGetValue("nested", out var next) ? next : null;
        }

        nested.Should().BeNull();
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
        JsonConvert.DeserializeObject<ConditionConfiguration>(attributeJson.ToString())!.FlagEvaluationField.IsPresent.Should().BeFalse();
        JsonConvert.DeserializeObject<ConditionConfiguration>(dependencyJson.ToString())!.AttributeField.IsPresent.Should().BeFalse();
    }

    [Fact]
    public void ConditionSerializationRetainsExplicitNullForValidation()
    {
        var condition = new ConditionConfiguration { Attribute = null, Operator = ConditionOperator.ONE_OF, Value = new[] { "paid" } };

        var json = JObject.Parse(JsonConvert.SerializeObject(condition));
        var roundTrip = JsonConvert.DeserializeObject<ConditionConfiguration>(json.ToString())!;

        json.Should().ContainKey(nameof(ConditionConfiguration.Attribute));
        json[nameof(ConditionConfiguration.Attribute)]!.Type.Should().Be(JTokenType.Null);
        roundTrip.AttributeField.IsPresent.Should().BeTrue();
        roundTrip.FlagEvaluationField.IsPresent.Should().BeFalse();
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
        var cycleTrace = Evaluator(cycle).EvaluateWithTrace("a", ValueType.String, "fallback", new EvaluationContext("subject"));
        var cycleResult = cycleTrace.Details;

        cycleResult.Reason.Should().Be(EvaluationReason.Error);
        cycleResult.Error.Should().Be("GENERAL");
        cycleTrace.DependencyEvaluations.Select(item => item.Evaluation.FlagKey).Should().Equal("b");
        cycleTrace.DependencyExposures.Should().BeEmpty();

        var depth = Configuration(
            DependentFlag("root", Allocation("root", [Dependency("middle", "on")], "on")),
            DependentFlag("middle", Allocation("middle", [Dependency("leaf", "on")], "on")),
            StaticFlag("leaf", "on"));
        depth.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = 1 };
        var trace = Evaluator(depth).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));
        trace.Details.Reason.Should().Be(EvaluationReason.Error);
        trace.DependencyEvaluations.Select(e => e.Evaluation.FlagKey).Should().Equal("middle", "leaf");
        trace.DependencyEvaluations.Should().OnlyContain(e => e.Evaluation.FlagMetadata!["errorCode"] == "GENERAL");

        depth.EvaluatorParams.MaxDependencyDepth = 2;
        Evaluator(depth).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");
    }

    [Fact]
    public void ReturnedBatchMaterializesCompletedSlotsInTraversalOrder()
    {
        var config = Configuration(
            DependentFlag("root", Allocation("root", [Dependency("middle", "on")], "on")),
            DependentFlag("middle", Allocation("middle", [Dependency("leaf", "on")], "on")),
            StaticFlag("leaf", "on"));
        config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = 1 };

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        trace.Details.Reason.Should().Be(EvaluationReason.Error);
        trace.DependencyEvaluations.Should().NotContainNulls();
        trace.DependencyEvaluations.Select(item => item.Evaluation.FlagKey).Should().Equal("middle", "leaf");
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(0, true, false)]
    [InlineData(255, false, false)]
    [InlineData(-1, false, true)]
    [InlineData(256, false, true)]
    [InlineData(1.5, false, true)]
    [InlineData("invalid", false, true)]
    public void EvaluatorParamsAreOptionalAndValidated(object? maxDepth, bool expectError, bool expectLog)
    {
        var config = Configuration(
            StaticFlag("dependency", "on"),
            DependentFlag("root", Allocation("root", [Dependency("dependency", "on")], "on")));
        if (maxDepth is not null)
        {
            config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = maxDepth };
        }

        var logger = new Mock<IDatadogLogger>();
        var trace = new FeatureFlagsEvaluator(null, config, logger: logger.Object)
                   .EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));
        var result = trace.Details;

        (result.Reason == EvaluationReason.Error).Should().Be(expectError);
        trace.DependencyEvaluations.Should().ContainSingle();
        logger.Invocations.Count(IsMalformedEvaluatorOptionsDiagnostic).Should().Be(expectLog ? 1 : 0);
    }

    [Fact]
    public void InvalidMaximumDepthDoesNotReportRootSelfReferenceAsDependency()
    {
        var config = Configuration(
            DependentFlag("root", Allocation("root", [Dependency("root", "on")], "on")));
        config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = "invalid" };

        var trace = Evaluator(config).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        trace.Details.Error.Should().Be("GENERAL");
        trace.DependencyEvaluations.Should().BeEmpty();
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
    public void IndependentFastPathMatchesExplicitTraceAndDependencyPathOptsOut()
    {
        var independent = Evaluator(Configuration(StaticFlag("root", "on")));

        independent.TryEvaluateIndependent("root", ValueType.String, "fallback", new EvaluationContext("subject"), out var fast).Should().BeTrue();
        var traced = independent.EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject")).Details;
        fast.Should().NotBeNull();
        fast!.Value.Should().Be(traced.Value);
        fast.Reason.Should().Be(traced.Reason);
        fast.Variant.Should().Be(traced.Variant);
        fast.Error.Should().Be(traced.Error);

        var dependent = Evaluator(Configuration(
            StaticFlag("parent", "on"),
            DependentFlag("root", Allocation("root", [Dependency("parent", "on")], "on"))));
        dependent.TryEvaluateIndependent("root", ValueType.String, "fallback", new EvaluationContext("subject"), out _).Should().BeFalse();
    }

    [Fact]
    public void ExplicitNullMaximumDepthIsIgnoredWhenUnusedAndFallsBackWhenUsed()
    {
        var config = Configuration(
            StaticFlag("ordinary", "on"),
            StaticFlag("dependency", "on"),
            DependentFlag("dependent", Allocation("dependent", [Dependency("dependency", "on")], "on")));
        config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = null };

        Evaluator(config).Evaluate("ordinary", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");
        var trace = Evaluator(config).EvaluateWithTrace("dependent", ValueType.String, "fallback", new EvaluationContext("subject"));
        var dependent = trace.Details;

        dependent.Reason.Should().NotBe(EvaluationReason.Error);
        dependent.Value.Should().Be("on");
        trace.DependencyEvaluations.Should().ContainSingle();
        trace.DependencyEvaluations[0].Evaluation.FlagKey.Should().Be("dependency");
    }

    [Fact]
    public void EvaluatorParameterPresenceSurvivesJsonRoundTrips()
    {
        var absent = RoundTrip(
            Configuration(
                StaticFlag("dependency", "on"),
                DependentFlag("root", Allocation("root", [Dependency("dependency", "on")], "on"))),
            out var absentJson);
        absentJson.Should().NotContainKey(nameof(ServerConfiguration.EvaluatorParams));
        absent.EvaluatorParamsField.IsPresent.Should().BeFalse();
        Evaluator(absent).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");

        var emptyInput = Configuration(
            StaticFlag("dependency", "on"),
            DependentFlag("root", Allocation("root", [Dependency("dependency", "on")], "on")));
        emptyInput.EvaluatorParams = new EvaluatorParams();
        var empty = RoundTrip(emptyInput, out var emptyJson);
        ((JObject)emptyJson[nameof(ServerConfiguration.EvaluatorParams)]!).Should().NotContainKey(nameof(EvaluatorParams.MaxDependencyDepth));
        empty.EvaluatorParamsField.IsPresent.Should().BeTrue();
        empty.EvaluatorParams!.MaxDependencyDepthField.IsPresent.Should().BeFalse();
        Evaluator(empty).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");

        var nullDepthInput = Configuration(
            StaticFlag("dependency", "on"),
            DependentFlag("root", Allocation("root", [Dependency("dependency", "on")], "on")));
        nullDepthInput.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = null };
        var nullDepth = RoundTrip(nullDepthInput, out var nullDepthJson);
        nullDepthJson[nameof(ServerConfiguration.EvaluatorParams)]![nameof(EvaluatorParams.MaxDependencyDepth)]!.Type.Should().Be(JTokenType.Null);
        nullDepth.EvaluatorParams!.MaxDependencyDepthField.IsPresent.Should().BeTrue();
        Evaluator(nullDepth).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");

        var nullParamsInput = Configuration(
            StaticFlag("dependency", "on"),
            DependentFlag("root", Allocation("root", [Dependency("dependency", "on")], "on")));
        nullParamsInput.EvaluatorParams = null;
        var nullParams = RoundTrip(nullParamsInput, out var nullParamsJson);
        nullParamsJson[nameof(ServerConfiguration.EvaluatorParams)]!.Type.Should().Be(JTokenType.Null);
        nullParams.EvaluatorParamsField.IsPresent.Should().BeTrue();
        Evaluator(nullParams).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");

        static ServerConfiguration RoundTrip(ServerConfiguration configuration, out JObject json)
        {
            json = JObject.Parse(JsonConvert.SerializeObject(configuration));
            return JsonConvert.DeserializeObject<ServerConfiguration>(json.ToString())!;
        }
    }

    [Theory]
    [CombinatorialData]
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
        trace.DependencyEvaluations.Select(e => e.Evaluation.FlagKey).Should().Equal(
            sharedSubtreeFirst ? ["a", "leaf", "b"] : ["b", "a", "leaf"]);
        trace.DependencyEvaluations.Select(e => e.Evaluation.FlagKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void OmittedMaximumDepthAllowsExactlyTenEdgesAndRejectsEleven()
    {
        Evaluator(DependencyChain(10)).Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"))
                                      .Value.Should().Be("on");

        var trace = Evaluator(DependencyChain(11)).EvaluateWithTrace("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        trace.Details.Reason.Should().Be(EvaluationReason.Error);
        trace.Details.Error.Should().Be("GENERAL");
        trace.DependencyEvaluations.Should().HaveCount(11);
        trace.DependencyExposures.Should().BeEmpty();
    }

    [Fact]
    public void MaximumDepthIsResolvedForEachRootEvaluation()
    {
        var config = Configuration(
            DependentFlag("root", Allocation("root", [Dependency("middle", "on")], "on")),
            DependentFlag("middle", Allocation("middle", [Dependency("leaf", "on")], "on")),
            StaticFlag("leaf", "on"));
        config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = 1 };
        var evaluator = Evaluator(config);

        evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Reason.Should().Be(EvaluationReason.Error);

        config.EvaluatorParams.MaxDependencyDepth = 2;

        evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");
    }

    [Fact]
    public void MalformedAndThrowingEvaluatorOptionsLogDebugOncePerRootAndFallBackLazily()
    {
        var options = new EvaluatorParams?[]
        {
            null,
            EvaluatorParams.Invalid,
            new() { MaxDependencyDepth = new ThrowingConvertible() },
        };

        foreach (var option in options)
        {
            var logger = new Mock<IDatadogLogger>();
            var config = Configuration(
                StaticFlag("ordinary", "ordinary"),
                StaticFlag("left", "on"),
                StaticFlag("right", "on"),
                DependentFlag("root", Allocation("root", [Dependency("left", "on"), Dependency("right", "on")], "on")));
            config.EvaluatorParams = option;
            var evaluator = new FeatureFlagsEvaluator(null, config, logger: logger.Object);

            evaluator.Evaluate("ordinary", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("ordinary");
            logger.Invocations.Should().NotContain(invocation => IsMalformedEvaluatorOptionsDiagnostic(invocation));
            evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");
            evaluator.Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject")).Value.Should().Be("on");
            logger.Invocations.Count(IsMalformedEvaluatorOptionsDiagnostic).Should().Be(2);
        }
    }

    [Fact]
    public void EvaluatorOptionDiagnosticFailureDoesNotAffectEvaluation()
    {
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(x => x.Debug(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
              .Throws(new InvalidOperationException("logging failed"));
        var config = Configuration(
            StaticFlag("dependency", "on"),
            DependentFlag("root", Allocation("root", [Dependency("dependency", "on")], "on")));
        config.EvaluatorParams = new EvaluatorParams { MaxDependencyDepth = "invalid" };

        var result = new FeatureFlagsEvaluator(null, config, logger: logger.Object)
                    .Evaluate("root", ValueType.String, "fallback", new EvaluationContext("subject"));

        result.Value.Should().Be("on");
        result.Reason.Should().NotBe(EvaluationReason.Error);
    }

    [Fact]
    public void MemoizationDeduplicatesActualPrerequisiteEvaluation()
    {
        var reads = new CountingValue("paid");
        var parent = DependentFlag("parent", Allocation("parent", [Attribute("plan", "paid")], "on"));
        var root = DependentFlag(
            "root",
            Allocation("root", [Dependency("parent", "on"), Dependency("parent", "on")], "on"));

        var trace = Evaluator(Configuration(parent, root)).EvaluateWithTrace(
            "root",
            ValueType.String,
            "fallback",
            new EvaluationContext("subject", new Dictionary<string, object?> { ["plan"] = reads }));

        trace.Details.Value.Should().Be("on");
        reads.Count.Should().Be(1);
        trace.DependencyEvaluations.Select(item => item.Evaluation.FlagKey).Should().Equal("parent");
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

    private static bool IsMalformedEvaluatorOptionsDiagnostic(IInvocation invocation) =>
        invocation.Method.Name == nameof(IDatadogLogger.Debug)
     && invocation.Arguments.Count > 1
     && invocation.Arguments[1] is string message
     && message == "Feature Flags evaluator options are malformed; using the default maximum dependency depth of 10";

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

    private static ServerConfiguration DependencyChain(int edges)
    {
        var flags = new List<Flag> { StaticFlag("node-" + edges, "on") };
        for (var i = edges - 1; i >= 0; i--)
        {
            var key = i == 0 ? "root" : "node-" + i;
            flags.Add(DependentFlag(key, Allocation(key, [Dependency("node-" + (i + 1), "on")], "on")));
        }

        return Configuration(flags.ToArray());
    }

    private sealed class CountingValue(string value)
    {
        public int Count { get; private set; }

        public override string ToString()
        {
            Count++;
            return value;
        }
    }

    private sealed class ThrowingConvertible : IConvertible
    {
        public TypeCode GetTypeCode() => TypeCode.Object;

        public bool ToBoolean(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public byte ToByte(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public char ToChar(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public DateTime ToDateTime(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public decimal ToDecimal(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public double ToDouble(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public short ToInt16(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public int ToInt32(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public long ToInt64(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public sbyte ToSByte(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public float ToSingle(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public string ToString(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public object ToType(Type conversionType, IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public ushort ToUInt16(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public uint ToUInt32(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");

        public ulong ToUInt64(IFormatProvider? provider) => throw new InvalidOperationException("conversion failed");
    }
}
