// <copyright file="FlagEvaluationApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary>
/// Mechanical unit tests for the EVP <c>flagevaluation</c> aggregation, payload, and transport contract.
/// </summary>
public class FlagEvaluationApiTests
{
    private const long SchemaValidTimestampMs = 1_800_000_000_000L;

    // -------------------------------------------------------------------------
    // Canonical context key — type-tagged, length-delimited, comparable, no hash
    // -------------------------------------------------------------------------

    [Fact]
    public void CanonicalContextKey_EmptyAttrs_ReturnsEmpty()
    {
        FlagEvaluationAggregator.CanonicalContextKey(new Dictionary<string, object?>()).Should().BeEmpty();
    }

    [Fact]
    public void CanonicalContextKey_NullAttrs_ReturnsEmpty()
    {
        FlagEvaluationAggregator.CanonicalContextKey(null).Should().BeEmpty();
    }

    [Fact]
    public void CanonicalContextKey_IntVsStringOne_ProducesDistinctKeys()
    {
        // int 1 and string "1" must encode differently (type tag), so they never alias in one bucket.
        var intKey = FlagEvaluationAggregator.CanonicalContextKey(new Dictionary<string, object?> { ["x"] = 1 });
        var strKey = FlagEvaluationAggregator.CanonicalContextKey(new Dictionary<string, object?> { ["x"] = "1" });

        intKey.Should().NotBeEmpty();
        strKey.Should().NotBeEmpty();
        intKey.Should().NotBe(strKey, "int 1 and string \"1\" must produce distinct canonical keys");
    }

    [Fact]
    public void CanonicalContextKey_DelimiterBearingValues_DoNotAlias()
    {
        // Length-prefixed encoding: a value that contains the field/key separator characters must
        // not be able to forge a different field boundary into the same encoded key.
        var a = FlagEvaluationAggregator.CanonicalContextKey(new Dictionary<string, object?> { ["a"] = "b=c", ["d"] = "e" });
        var b = FlagEvaluationAggregator.CanonicalContextKey(new Dictionary<string, object?> { ["a"] = "b", ["c=d"] = "e" });

        a.Should().NotBe(b, "length-delimited fields prevent value/key separator forgery");
    }

    [Fact]
    public void CanonicalContextKey_SameAttrsDifferentOrder_SameKey()
    {
        // Deterministic regardless of dictionary insertion/iteration order (keys sorted before encode).
        var attrs1 = new Dictionary<string, object?> { ["a"] = "x", ["b"] = "y" };
        var attrs2 = new Dictionary<string, object?> { ["b"] = "y", ["a"] = "x" };

        FlagEvaluationAggregator.CanonicalContextKey(attrs1)
            .Should().Be(FlagEvaluationAggregator.CanonicalContextKey(attrs2));
    }

    // -------------------------------------------------------------------------
    // G6 — Context pruning is deterministic + 256 fields / 256 chars
    // -------------------------------------------------------------------------

    [Fact]
    public void PruneContext_Over256Fields_KeepsFirst256Sorted()
    {
        var attrs = new Dictionary<string, object?>();
        for (int i = 0; i < 300; i++)
        {
            attrs[$"field_{i:D3}"] = $"value_{i}";
        }

        var pruned = FlagEvaluationAggregator.PruneContext(attrs);
        pruned.Should().HaveCount(256);
        // Deterministic ordinal sort, then cut: the first 256 keys alphabetically survive.
        pruned.ContainsKey("field_000").Should().BeTrue();
        pruned.ContainsKey("field_255").Should().BeTrue();
        pruned.ContainsKey("field_256").Should().BeFalse();
    }

    [Fact]
    public void PruneContext_StringValueOver256Chars_FieldSkipped()
    {
        var longValue = new string('x', 257);
        var attrs = new Dictionary<string, object?> { ["a"] = "ok", ["b"] = longValue };

        var pruned = FlagEvaluationAggregator.PruneContext(attrs);
        pruned.ContainsKey("a").Should().BeTrue();
        pruned.ContainsKey("b").Should().BeFalse("string values over 256 chars are skipped");
    }

    [Fact]
    public void PruneContext_IsDeterministic_AcrossRepeatedCalls()
    {
        // The kept subset must be identical across runs so logically-identical contexts always
        // prune to the same keys (and therefore the same canonical key / bucket).
        Dictionary<string, object?> Build()
        {
            var d = new Dictionary<string, object?>();
            for (int i = 0; i < 400; i++)
            {
                d[$"k{i:D3}"] = i;
            }

            return d;
        }

        var first = FlagEvaluationAggregator.PruneContext(Build());
        var second = FlagEvaluationAggregator.PruneContext(Build());

        first.Keys.Should().BeEquivalentTo(second.Keys, "pruning is deterministic");
    }

    [Fact]
    public void Aggregator_FullTierBucket_StoresPrunedContextNotRaw()
    {
        // The emitted full-tier bucket must carry the PRUNED attributes (oversized field removed),
        // not the raw context the hook captured.
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ctx = new Dictionary<string, object?> { ["keep"] = "ok", ["drop"] = new string('y', 300) };
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc", "user", t, ctx));

        var entry = agg.Drain().Full.Values.Should().ContainSingle().Subject;
        entry.ContextAttrs.Should().NotBeNull();
        entry.ContextAttrs!.ContainsKey("keep").Should().BeTrue();
        entry.ContextAttrs.ContainsKey("drop").Should().BeFalse("oversized string is pruned from the stored payload context");
    }

    // -------------------------------------------------------------------------
    // Two-tier aggregation, caps, drop-counted overflow, runtime-default
    // -------------------------------------------------------------------------

    [Fact]
    public void Aggregator_TwoIdenticalEvents_OneBucketCountTwo_FirstLastMinMax()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long t2 = t1 + 100;

        var ctx = new Dictionary<string, object?> { ["env"] = "prod" };
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t1, new Dictionary<string, object?>(ctx)));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t2, new Dictionary<string, object?>(ctx)));

        var drained = agg.Drain();
        drained.Full.Should().HaveCount(1, "identical dims + context collapse into one full-tier bucket");
        var bucket = drained.Full.Values.Should().ContainSingle().Subject;
        bucket.Count.Should().Be(2);
        bucket.FirstEvaluationMs.Should().Be(t1, "first_evaluation is the min eval time, not wall-clock");
        bucket.LastEvaluationMs.Should().Be(t2, "last_evaluation is the max eval time, not wall-clock");
        drained.Degraded.Should().BeEmpty();
    }

    [Fact]
    public void Aggregator_OutOfOrderEvalTimes_FirstLastUseMinMax()
    {
        // first/last must be min/max even when the later-arriving event has the smaller timestamp.
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long late = 5_000;
        long early = 1_000;

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc", "user", late, null));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc", "user", early, null));

        var bucket = agg.Drain().Full.Values.Should().ContainSingle().Subject;
        bucket.FirstEvaluationMs.Should().Be(early);
        bucket.LastEvaluationMs.Should().Be(late);
    }

    [Fact]
    public void Aggregator_DifferentContextValueTypes_TwoDistinctFullBuckets()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["count"] = 1 }));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["count"] = "1" }));

        var drained = agg.Drain();
        drained.Full.Should().HaveCount(2, "int 1 and string \"1\" must land in distinct full-tier buckets");
        drained.Degraded.Should().BeEmpty();
    }

    [Fact]
    public void Aggregator_FullTierOverflowPastGlobalCap_RoutesToDegraded()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 2, perFlagCap: 100, degradedCap: 100);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["ctx"] = "v1" }));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-2", t, new Dictionary<string, object?> { ["ctx"] = "v2" }));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-3", t, new Dictionary<string, object?> { ["ctx"] = "v3" }));

        var drained = agg.Drain();
        drained.Full.Should().HaveCount(2);
        drained.Degraded.Should().HaveCount(1, "overflow from the full tier routes to degraded");
        drained.Dropped.Should().Be(0);
    }

    [Fact]
    public void Aggregator_FullTierOverflowPastPerFlagCap_RoutesToDegraded()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 100, perFlagCap: 2, degradedCap: 100);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["ctx"] = "v1" }));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-2", t, new Dictionary<string, object?> { ["ctx"] = "v2" }));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-3", t, new Dictionary<string, object?> { ["ctx"] = "v3" }));

        var drained = agg.Drain();
        drained.Full.Should().HaveCount(2);
        drained.Degraded.Should().HaveCount(1, "per-flag overflow routes to degraded");
        drained.Dropped.Should().Be(0);
    }

    [Fact]
    public void Aggregator_DegradedTierOverflowPastDegradedCap_IncrementsDroppedCounter()
    {
        // Beyond degradedCap (the terminal tier) the count is dropped and counted observably.
        var agg = new FlagEvaluationAggregator(globalCap: 0, perFlagCap: 0, degradedCap: 2);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, null));
        agg.Add(new FlagEvalEvent("flag-a", "off", "alloc-1", "user-2", t, null));
        agg.Add(new FlagEvalEvent("flag-b", "on", "alloc-1", "user-3", t, null)); // over cap → dropped
        agg.Add(new FlagEvalEvent("flag-c", "on", "alloc-1", "user-4", t, null)); // over cap → dropped

        var drained = agg.Drain();
        drained.Full.Should().BeEmpty();
        drained.Degraded.Should().HaveCount(2, "degraded cap is 2; the first 2 distinct keys fill it");
        drained.Dropped.Should().Be(2, "two evaluations dropped on degraded-cap overflow");
    }

    [Fact]
    public void Aggregator_DegradedCapDrop_DoesNotLoseExistingBucketCounts()
    {
        // Once degraded is at cap, a NEW key is dropped but an EXISTING key keeps counting.
        var agg = new FlagEvaluationAggregator(globalCap: 0, perFlagCap: 0, degradedCap: 1);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc", "user", t, null));      // fills cap
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc", "user", t + 1, null));  // existing key → counted
        agg.Add(new FlagEvalEvent("flag-b", "on", "alloc", "user", t, null));      // new key over cap → dropped

        var drained = agg.Drain();
        drained.Degraded.Should().HaveCount(1);
        drained.Degraded.Values.Should().ContainSingle().Which.Count.Should().Be(2);
        drained.Dropped.Should().Be(1);
    }

    [Fact]
    public void Aggregator_AbsentVariant_RuntimeDefaultTrue()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", null, "alloc-1", "user-1", t, null));

        agg.Drain().Full.Values.Should().ContainSingle().Which.RuntimeDefault
            .Should().BeTrue("an absent variant means the runtime default was used");
    }

    [Fact]
    public void Aggregator_PresentVariant_RuntimeDefaultFalse()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, null));

        agg.Drain().Full.Values.Should().ContainSingle().Which.RuntimeDefault
            .Should().BeFalse("a present variant means the runtime default was not used");
    }

    // -------------------------------------------------------------------------
    // BuildPayload — required fields, degraded omission, drain
    // -------------------------------------------------------------------------

    [Fact]
    public void FlagEvaluationPath_IsCorrect()
    {
        FlagEvaluationApi.FlagEvaluationPath.Should().Be("evp_proxy/v2/api/v2/flagevaluations");
    }

    [Fact]
    public void BuildPayload_EmptyAggregator_ReturnsNull()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        FlagEvaluationApi.BuildPayload(agg, service: "svc", env: "prod", version: "1.0").Should().BeNull();
    }

    [Fact]
    public void BuildPayload_FullTierBucket_HasRequiredFields()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, null));

        var payload = FlagEvaluationApi.BuildPayload(agg, service: "svc", env: "prod", version: "1.0");
        payload.Should().NotBeNull();
        payload!.FlagEvaluations.Should().HaveCount(1);

        var ev = payload.FlagEvaluations[0];
        ev.Flag.Key.Should().Be("flag-a");
        ev.EvaluationCount.Should().Be(1);
        ev.FirstEvaluation.Should().Be(t);
        ev.LastEvaluation.Should().Be(t);
        ev.Timestamp.Should().Be(t);
    }

    [Fact]
    public void BuildPayload_FullEvent_IncludesAllocationAndTargetingKey()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        agg.Add(new FlagEvalEvent("flag", "control", "bucket_7", "user_42", 2_000L, null));

        var ev = FlagEvaluationApi.BuildPayload(agg, "svc", "env", "v")!.FlagEvaluations[0];
        ev.Allocation.Should().NotBeNull();
        ev.Allocation!.Key.Should().Be("bucket_7");
        ev.Variant.Should().NotBeNull();
        ev.Variant!.Key.Should().Be("control");
        ev.TargetingKey.Should().Be("user_42");
    }

    [Fact]
    public void BuildPayload_DegradedBucket_OmitsTargetingKeyAndContext()
    {
        // globalCap=0/perFlagCap=0 forces the degraded tier; it must drop targeting_key + context.
        var agg = new FlagEvaluationAggregator(globalCap: 0, perFlagCap: 0, degradedCap: 10);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["env"] = "prod" }));

        var ev = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!.FlagEvaluations[0];
        ev.Flag.Key.Should().Be("flag-a");
        ev.Context.Should().BeNull("degraded tier omits context");
        ev.TargetingKey.Should().BeNull("degraded tier omits targeting_key");
    }

    [Fact]
    public void BuildPayload_DrainsAggregatorAfterBuild()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, null));

        FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0");
        FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")
            .Should().BeNull("the aggregator is drained by BuildPayload");
    }

    // -------------------------------------------------------------------------
    // G3 — JSON wire format against the flageval-worker contract (production serializer)
    // -------------------------------------------------------------------------

    [Fact]
    public void Serialize_BatchKeyIsCamelCase_InnerFieldsSnakeCase()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = 1_700_000_000_000L;
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["env"] = "prod" }));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!;
        var json = FlagEvaluationApi.SerializeForTest(payload);
        var root = JObject.Parse(json);

        // Batch wrapper key is camelCase per the worker schema / Go reference.
        root["flagEvaluations"].Should().NotBeNull("batch wrapper key is camelCase");
        root["flag_evaluations"].Should().BeNull("the snake_case naming strategy must NOT apply to the batch key");

        // Inner event fields stay snake_case.
        var ev = (JObject)root["flagEvaluations"]![0]!;
        ev["first_evaluation"].Should().NotBeNull();
        ev["last_evaluation"].Should().NotBeNull();
        ev["evaluation_count"].Should().NotBeNull();
    }

    [Fact]
    public void Serialize_VariantAndAllocation_AreObjectsWithKey_NotBareStrings()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", SchemaValidTimestampMs, null));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!;
        var ev = (JObject)JObject.Parse(FlagEvaluationApi.SerializeForTest(payload))["flagEvaluations"]![0]!;

        ev["variant"]!.Type.Should().Be(JTokenType.Object, "variant serializes as { key } object, not a bare string");
        ev["variant"]!["key"]!.Value<string>().Should().Be("on");
        ev["flag"]!["key"]!.Value<string>().Should().Be("flag-a");
        ev["allocation"]!.Type.Should().Be(JTokenType.Object, "allocation serializes as { key } object, not a bare string");
        ev["allocation"]!["key"]!.Value<string>().Should().Be("alloc-1");
    }

    [Fact]
    public void Serialize_DegradedEvent_OmitsTargetingKeyAndContextKeys()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 0, perFlagCap: 0, degradedCap: 10);
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", SchemaValidTimestampMs, new Dictionary<string, object?> { ["env"] = "prod" }));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!;
        var ev = (JObject)JObject.Parse(FlagEvaluationApi.SerializeForTest(payload))["flagEvaluations"]![0]!;

        ev.ContainsKey("targeting_key").Should().BeFalse("degraded tier omits targeting_key from the JSON entirely");
        ev.ContainsKey("context").Should().BeFalse("degraded tier omits context from the JSON entirely");
    }

    [Fact]
    public void Serialize_RuntimeDefaultUsed_PresentOnlyWhenTrue()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        agg.Add(new FlagEvalEvent("flag-default", null, null, "user", SchemaValidTimestampMs, null));
        agg.Add(new FlagEvalEvent("flag-present", "on", "alloc", "user", SchemaValidTimestampMs, null));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!;
        var json = JObject.Parse(FlagEvaluationApi.SerializeForTest(payload));

        JObject? defaultEv = null;
        JObject? presentEv = null;
        foreach (var token in json["flagEvaluations"]!)
        {
            var o = (JObject)token;
            if (o["flag"]!["key"]!.Value<string>() == "flag-default")
            {
                defaultEv = o;
            }
            else
            {
                presentEv = o;
            }
        }

        defaultEv!["runtime_default_used"]!.Value<bool>().Should().BeTrue();
        presentEv!.ContainsKey("runtime_default_used").Should().BeFalse("the field is omitted when not a runtime default");
    }

    [Fact]
    public void Serialize_FullAndDegradedEvents_ValidateAgainstWorkerSchema()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 1, perFlagCap: 10, degradedCap: 10);
        agg.Add(new FlagEvalEvent("flag-full", "on", "alloc-1", "user-1", SchemaValidTimestampMs, new Dictionary<string, object?> { ["env"] = "prod" }));
        agg.Add(new FlagEvalEvent("flag-degraded", "off", "alloc-2", "user-2", SchemaValidTimestampMs + 1, new Dictionary<string, object?> { ["env"] = "prod" }));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!;
        var json = JObject.Parse(FlagEvaluationApi.SerializeForTest(payload));

        AssertValidAgainstWorkerSchema(json);
    }

    [Fact]
    public void WorkerSchemaRejectsTopLevelReason()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 10, degradedCap: 10);
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", SchemaValidTimestampMs, null));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!;
        var json = JObject.Parse(FlagEvaluationApi.SerializeForTest(payload));
        ((JObject)json["flagEvaluations"]![0]!).Add("reason", "targeting_match");

        var errors = ValidateAgainstWorkerSchema(json);
        errors.Should().Contain(e => e.IndexOf("reason", StringComparison.Ordinal) >= 0);
    }

    [Fact]
    public void FlagEvalEventDoesNotCarryReason()
    {
        typeof(FlagEvalEvent).GetProperty("Reason").Should().BeNull("OpenFeature reason is not an EVP schema-visible dimension");
    }

    [Fact]
    public void Aggregator_ReasonOnlyDifferencesCollapseIntoOneSchemaVisibleBucket()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 10, degradedCap: 10);

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", SchemaValidTimestampMs, null));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", SchemaValidTimestampMs + 1, null));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!;
        payload.FlagEvaluations.Should().ContainSingle()
            .Which.EvaluationCount.Should().Be(2, "hidden OpenFeature reason cannot split backend-indistinguishable EVP buckets");
    }

    [Fact]
    public void Aggregator_ErrorMessageSplitsSchemaVisibleBuckets()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 10, degradedCap: 10);

        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", SchemaValidTimestampMs, null, errorMessage: "provider_not_ready"));
        agg.Add(new FlagEvalEvent("flag-a", "on", "alloc-1", "user-1", SchemaValidTimestampMs + 1, null, errorMessage: "type_mismatch"));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "prod", "1.0")!;
        payload.FlagEvaluations.Should().HaveCount(2);
        payload.FlagEvaluations.Should().OnlyContain(ev => ev.Error != null);
    }

    [Fact]
    public void FlagEvalEvent_QueuesPrunedContextSnapshot()
    {
        var attrs = new Dictionary<string, object?>();
        for (int i = 0; i < 300; i++)
        {
            attrs[$"field_{i:D3}"] = $"value_{i}";
        }

        attrs["oversized"] = new string('x', 300);

        var ev = new FlagEvalEvent("flag-a", "on", "alloc", "user", SchemaValidTimestampMs, attrs);
        var contextAttrs = ev.ContextAttrs;
        contextAttrs.Should().NotBeNull();
        var boundedAttrs = contextAttrs!;
        boundedAttrs.Should().HaveCount(256);
        boundedAttrs.ContainsKey("field_000").Should().BeTrue();
        boundedAttrs.ContainsKey("field_256").Should().BeFalse();
        boundedAttrs.ContainsKey("oversized").Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // G2 — async boundary: the hot-path Enqueue does NOT aggregate; the worker drain does
    // -------------------------------------------------------------------------

    [Fact]
    public void Enqueue_DoesNotAggregateOnHotPath_UntilDrained()
    {
        var api = NewApiWithCapturedTransport(out _);

        api.EnqueueForTest(new FlagEvalEvent("flag-a", "on", "alloc", "user", 1_000L, null));
        api.PendingQueueCount.Should().Be(1, "the hot path only enqueues; nothing is aggregated yet");

        api.DrainQueueIntoAggregator();
        api.PendingQueueCount.Should().Be(0, "the worker drain moved the event off the queue into the aggregator");
    }

    // -------------------------------------------------------------------------
    // G4 — backpressure: queue overflow drops and counts observably
    // -------------------------------------------------------------------------

    [Fact]
    public void Enqueue_BeyondQueueCapacity_DropsAndCounts()
    {
        var api = NewApiWithCapturedTransport(out _);

        // Fill the bounded queue exactly to capacity, then push one more.
        for (int i = 0; i < FlagEvaluationApi.QueueCapacity; i++)
        {
            api.EnqueueForTest(new FlagEvalEvent("flag", "on", "alloc", "user", 1_000L, null));
        }

        api.PendingQueueCount.Should().Be(FlagEvaluationApi.QueueCapacity);
        api.DroppedBackpressureCount.Should().Be(0, "nothing dropped while at/under capacity");

        api.EnqueueForTest(new FlagEvalEvent("flag", "on", "alloc", "user", 1_000L, null));
        api.DroppedBackpressureCount.Should().Be(1, "the overflow event is dropped and counted observably");
        api.PendingQueueCount.Should().Be(FlagEvaluationApi.QueueCapacity, "the queue is not allowed to grow past capacity");
    }

    [Fact]
    public async Task FlushAsync_ResetsBackpressureDropCounterAfterReporting()
    {
        var api = NewApiWithCapturedTransport(out _);
        for (int i = 0; i <= FlagEvaluationApi.QueueCapacity; i++)
        {
            api.EnqueueForTest(new FlagEvalEvent("flag", "on", "alloc", "user", 1_000L, null));
        }

        api.DroppedBackpressureCount.Should().Be(1);
        await api.FlushAsync();
        api.DroppedBackpressureCount.Should().Be(0, "the backpressure count is reset once surfaced on flush");
    }

    // -------------------------------------------------------------------------
    // G5 — shutdown drains the queue and flushes a final batch before exit
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ShutdownSendLoop_DrainsQueueAndSendsFinalBatch()
    {
        var api = NewApiWithCapturedTransport(out var capture);

        api.EnqueueForTest(new FlagEvalEvent("flag-shutdown", "on", "alloc", "user", 1_000L, null));

        // RunSendLoopForTestAsync sets the process-exit gate then runs the loop, so only the
        // final shutdown drain/flush executes — proving shutdown does not lose buffered events.
        await api.RunSendLoopForTestAsync();

        capture.LastPayload.Should().NotBeNull("the final shutdown flush must send the queued event");
        capture.LastPayload!.FlagEvaluations.Should().ContainSingle()
            .Which.Flag.Key.Should().Be("flag-shutdown");
        api.PendingQueueCount.Should().Be(0, "the queue is drained on shutdown");
    }

    [Fact]
    public async Task FlushAsync_DrainsQueueIntoPostedPayload()
    {
        var api = NewApiWithCapturedTransport(out var capture);
        api.EnqueueForTest(new FlagEvalEvent("flag-a", "on", "alloc", "user", 1_000L, new Dictionary<string, object?> { ["env"] = "prod" }));
        api.EnqueueForTest(new FlagEvalEvent("flag-b", "off", null, "user", 1_000L, null));

        var sent = await api.FlushAsync();

        sent.Should().BeTrue();
        capture.LastPayload.Should().NotBeNull();
        capture.LastPayload!.FlagEvaluations.Should().HaveCount(2);
    }

    /// <summary>
    /// Builds a FlagEvaluationApi over a mocked transport that captures the posted
    /// <see cref="FlagEvaluationsRequest"/> so tests can assert what was sent.
    /// </summary>
    private static FlagEvaluationApi NewApiWithCapturedTransport(out PayloadCapture capture)
    {
        var localCapture = new PayloadCapture();

        var responseMock = new Mock<IApiResponse>();
        responseMock.Setup(x => x.StatusCode).Returns(200);

        var requestMock = new Mock<IApiRequest>();
        requestMock
            .Setup(x => x.PostAsJsonAsync(It.IsAny<FlagEvaluationsRequest>(), It.IsAny<MultipartCompression>(), It.IsAny<JsonSerializerSettings>()))
            .Callback<FlagEvaluationsRequest, MultipartCompression, JsonSerializerSettings>((payload, compression, settings) => localCapture.LastPayload = payload)
            .ReturnsAsync(responseMock.Object);

        var factoryMock = new Mock<IApiRequestFactory>();
        factoryMock.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns(new Uri("http://localhost/flagevaluations"));
        factoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

        capture = localCapture;
        return new FlagEvaluationApi(factoryMock.Object, service: "svc", env: "prod", version: "1.0");
    }

    private static void AssertValidAgainstWorkerSchema(JObject json)
    {
        var errors = ValidateAgainstWorkerSchema(json);
        errors.Should().BeEmpty(string.Join(Environment.NewLine, errors));
    }

    private static List<string> ValidateAgainstWorkerSchema(JToken json)
    {
        var schema = LoadWorkerSchema();
        var errors = new List<string>();
        ValidateSchema(json, schema, "$", errors);
        return errors;
    }

    private static JObject LoadWorkerSchema()
    {
        var resourceName = typeof(FlagEvaluationApiTests).Assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith("batchedflagevaluations.json", StringComparison.Ordinal));
        using var stream = typeof(FlagEvaluationApiTests).Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return JObject.Parse(reader.ReadToEnd());
    }

    private static void ValidateSchema(JToken value, JObject schema, string path, List<string> errors)
    {
        if (schema["type"] is JValue typeToken && !MatchesType(value, typeToken.Value<string>()))
        {
            errors.Add($"{path}: expected {typeToken.Value<string>()}, got {value.Type}");
            return;
        }

        if (schema["minimum"] is JValue minimum && value.Type == JTokenType.Integer && value.Value<long>() < minimum.Value<long>())
        {
            errors.Add($"{path}: value {value.Value<long>()} is below minimum {minimum.Value<long>()}");
        }

        if (schema["type"]?.Value<string>() == "array" && schema["items"] is JObject itemSchema && value is JArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                ValidateSchema(array[i], itemSchema, $"{path}[{i}]", errors);
            }

            return;
        }

        if (schema["type"]?.Value<string>() != "object" || value is not JObject obj)
        {
            return;
        }

        if (schema["required"] is JArray required)
        {
            foreach (string? requiredProperty in required.Values<string>())
            {
                if (requiredProperty is null)
                {
                    continue;
                }

                if (!obj.ContainsKey(requiredProperty))
                {
                    errors.Add($"{path}: missing required property {requiredProperty}");
                }
            }
        }

        var properties = schema["properties"] as JObject;
        if (properties is not null)
        {
            foreach (var property in obj.Properties())
            {
                if (properties[property.Name] is JObject propertySchema)
                {
                    ValidateSchema(property.Value, propertySchema, $"{path}.{property.Name}", errors);
                }
                else if (schema["additionalProperties"]?.Value<bool>() == false)
                {
                    errors.Add($"{path}: additional property {property.Name} is not allowed");
                }
            }
        }
    }

    private static bool MatchesType(JToken value, string? schemaType) => schemaType switch
    {
        "object" => value.Type == JTokenType.Object,
        "array" => value.Type == JTokenType.Array,
        "string" => value.Type == JTokenType.String,
        "integer" => value.Type == JTokenType.Integer,
        "boolean" => value.Type == JTokenType.Boolean,
        _ => true
    };

    private sealed class PayloadCapture
    {
        public FlagEvaluationsRequest? LastPayload { get; set; }
    }
}
