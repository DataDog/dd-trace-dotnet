// <copyright file="FlagEvaluationApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary> Unit tests for FlagEvaluationApi contract conformance. </summary>
public class FlagEvaluationApiTests
{
    // ---------------------------------------------------------------------------
    // CanonicalContextKey — type-tagged, length-delimited, comparable, not a hash
    // ---------------------------------------------------------------------------

    [Fact]
    public void CanonicalContextKey_EmptyAttrs_ReturnsEmpty()
    {
        var key = FlagEvaluationAggregator.CanonicalContextKey(new Dictionary<string, object?>());
        key.Should().BeEmpty();
    }

    [Fact]
    public void CanonicalContextKey_NullAttrs_ReturnsEmpty()
    {
        var key = FlagEvaluationAggregator.CanonicalContextKey(null);
        key.Should().BeEmpty();
    }

    [Fact]
    public void CanonicalContextKey_IntVsStringOneDistinctKeys()
    {
        // Reviewer concern #3: int 1 vs string "1" must produce DIFFERENT keys (type-tagged)
        var intKey = FlagEvaluationAggregator.CanonicalContextKey(new Dictionary<string, object?> { ["x"] = 1 });
        var strKey = FlagEvaluationAggregator.CanonicalContextKey(new Dictionary<string, object?> { ["x"] = "1" });

        intKey.Should().NotBeEmpty();
        strKey.Should().NotBeEmpty();
        intKey.Should().NotBe(strKey, "int 1 and string \"1\" must produce distinct canonical keys");
    }

    [Fact]
    public void CanonicalContextKey_SameAttrsDifferentOrder_SameKey()
    {
        // Canonical = deterministic regardless of dictionary iteration order
        var attrs1 = new Dictionary<string, object?> { ["a"] = "x", ["b"] = "y" };
        var attrs2 = new Dictionary<string, object?> { ["b"] = "y", ["a"] = "x" };

        var key1 = FlagEvaluationAggregator.CanonicalContextKey(attrs1);
        var key2 = FlagEvaluationAggregator.CanonicalContextKey(attrs2);

        key1.Should().Be(key2);
    }

    // ---------------------------------------------------------------------------
    // Context pruning: 256 fields / 256 chars (reviewer concern #1)
    // ---------------------------------------------------------------------------

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
        // Deterministic: should keep first 256 alphabetically-sorted keys
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
        pruned.ContainsKey("b").Should().BeFalse("string values >256 chars must be skipped");
    }

    // ---------------------------------------------------------------------------
    // Two-tier aggregation + caps + drop-counted overflow (reviewer concerns #5, #8)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Aggregator_TwoIdenticalEvents_OneBucketCountTwo_FirstLastMinMax()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long t2 = t1 + 100;

        var ev = new FlagEvalEvent(
            flagKey: "flag-a",
            variant: "on",
            reason: "targeting_match",
            allocationKey: "alloc-1",
            targetingKey: "user-1",
            evalTimeMs: t1,
            contextAttrs: new Dictionary<string, object?> { ["env"] = "prod" });

        var ev2 = new FlagEvalEvent(
            flagKey: "flag-a",
            variant: "on",
            reason: "targeting_match",
            allocationKey: "alloc-1",
            targetingKey: "user-1",
            evalTimeMs: t2,
            contextAttrs: new Dictionary<string, object?> { ["env"] = "prod" });

        agg.Add(ev);
        agg.Add(ev2);

        var (fullMap, degradedMap, _) = agg.Drain();
        fullMap.Should().HaveCount(1, "identical dims+context → one full-tier bucket");
        var bucket = fullMap.Values.Should().ContainSingle().Subject;
        bucket.Count.Should().Be(2);
        bucket.FirstEvaluationMs.Should().Be(t1);
        bucket.LastEvaluationMs.Should().Be(t2);
        degradedMap.Should().BeEmpty();
    }

    [Fact]
    public void Aggregator_TwoEventsWithDifferentContextValueTypes_TwoDistinctFullBuckets()
    {
        // Reviewer concern #3: int 1 vs string "1" in context → distinct full-tier buckets
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var evInt = new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["count"] = 1 });
        var evStr = new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["count"] = "1" });

        agg.Add(evInt);
        agg.Add(evStr);

        var (fullMap, degradedMap, _) = agg.Drain();
        fullMap.Should().HaveCount(2, "int 1 and string \"1\" must produce distinct full-tier buckets");
        degradedMap.Should().BeEmpty();
    }

    [Fact]
    public void Aggregator_FullTierOverflowPastGlobalCap_RoutesToDegraded()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 2, perFlagCap: 100, degradedCap: 100);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Fill globalCap with 2 distinct full buckets
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["ctx"] = "v1" }));
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-2", t, new Dictionary<string, object?> { ["ctx"] = "v2" }));

        // 3rd event should overflow to degraded
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-3", t, new Dictionary<string, object?> { ["ctx"] = "v3" }));

        var (fullMap, degradedMap, dropped) = agg.Drain();
        fullMap.Should().HaveCount(2);
        degradedMap.Should().HaveCount(1, "overflow from full tier routes to degraded");
        dropped.Should().Be(0);
    }

    [Fact]
    public void Aggregator_FullTierOverflowPastPerFlagCap_RoutesToDegraded()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 100, perFlagCap: 2, degradedCap: 100);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Fill perFlagCap with 2 distinct full buckets for same flag
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["ctx"] = "v1" }));
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-2", t, new Dictionary<string, object?> { ["ctx"] = "v2" }));

        // 3rd event for same flag should overflow to degraded
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-3", t, new Dictionary<string, object?> { ["ctx"] = "v3" }));

        var (fullMap, degradedMap, dropped) = agg.Drain();
        fullMap.Should().HaveCount(2);
        degradedMap.Should().HaveCount(1, "per-flag overflow routes to degraded");
        dropped.Should().Be(0);
    }

    [Fact]
    public void Aggregator_DegradedTierOverflowPastDegradedCap_IncrementsDroppedCounter()
    {
        // Reviewer concern #8: beyond degradedCap, increment dropped counter
        var agg = new FlagEvaluationAggregator(globalCap: 0, perFlagCap: 0, degradedCap: 2);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // All 4 events have different degraded keys (flag+variant+alloc+reason all distinct)
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-1", t, null));
        agg.Add(new FlagEvalEvent("flag-a", "off", "split", "alloc-1", "user-2", t, null));
        agg.Add(new FlagEvalEvent("flag-b", "on", "split", "alloc-1", "user-3", t, null)); // fills degradedCap
        agg.Add(new FlagEvalEvent("flag-c", "on", "split", "alloc-1", "user-4", t, null)); // should be dropped

        var (fullMap, degradedMap, dropped) = agg.Drain();
        fullMap.Should().BeEmpty();
        degradedMap.Should().HaveCount(2, "degraded cap is 2");
        dropped.Should().Be(1, "one event dropped due to degraded cap overflow");
    }

    [Fact]
    public void Aggregator_AbsentVariant_RuntimeDefaultTrue()
    {
        // Reviewer concern #5: absent variant (null) → runtime_default_used = true
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", variant: null, "default", "alloc-1", "user-1", t, null));

        var (fullMap, degradedMap, _) = agg.Drain();
        fullMap.Should().HaveCount(1);
        fullMap.Values.Should().ContainSingle().Which.RuntimeDefault.Should().BeTrue("absent variant means runtime_default_used=true");
    }

    [Fact]
    public void Aggregator_PresentVariant_RuntimeDefaultFalse()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        agg.Add(new FlagEvalEvent("flag-a", variant: "on", "targeting_match", "alloc-1", "user-1", t, null));

        var (fullMap, _, _) = agg.Drain();
        fullMap.Should().HaveCount(1);
        fullMap.Values.Should().ContainSingle().Which.RuntimeDefault.Should().BeFalse("present variant means runtime_default_used=false");
    }

    // ---------------------------------------------------------------------------
    // TryGetPayload — EVP path constant + schema shape
    // ---------------------------------------------------------------------------

    [Fact]
    public void FlagEvaluationPath_IsCorrect()
    {
        FlagEvaluationApi.FlagEvaluationPath.Should().Be("evp_proxy/v2/api/v2/flagevaluations");
    }

    [Fact]
    public void TryGetPayload_EmptyAggregator_ReturnsNull()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        var payload = FlagEvaluationApi.BuildPayload(agg, service: "svc", env: "prod", version: "1.0");
        payload.Should().BeNull("no events → nothing to flush");
    }

    [Fact]
    public void TryGetPayload_WithFullTierBucket_HasRequiredFields()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        agg.Add(new FlagEvalEvent("flag-a", "on", "targeting_match", "alloc-1", "user-1", t, null));

        var payload = FlagEvaluationApi.BuildPayload(agg, service: "svc", env: "prod", version: "1.0");
        payload.Should().NotBeNull();
        payload!.FlagEvaluations.Should().HaveCount(1);

        var ev = payload.FlagEvaluations[0];
        ev.Flag.Key.Should().Be("flag-a");
        ev.EvaluationCount.Should().Be(1);
        ev.FirstEvaluation.Should().Be(t);
        ev.LastEvaluation.Should().Be(t);
        ev.Timestamp.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryGetPayload_DegradedBucket_OmitsTargetingKeyAndContext()
    {
        // Reviewer concern #2 (NullValueHandling.Ignore): degraded tier omits targeting_key + context
        // We force degraded by setting globalCap=0, perFlagCap=0
        var agg = new FlagEvaluationAggregator(globalCap: 0, perFlagCap: 0, degradedCap: 10);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-1", t, new Dictionary<string, object?> { ["env"] = "prod" }));

        var payload = FlagEvaluationApi.BuildPayload(agg, service: "svc", env: "prod", version: "1.0");
        payload.Should().NotBeNull();
        payload!.FlagEvaluations.Should().HaveCount(1);

        var ev = payload.FlagEvaluations[0];
        ev.Flag.Key.Should().Be("flag-a");
        // Degraded: no context, no targeting_key
        ev.Context.Should().BeNull("degraded tier omits context");
        ev.TargetingKey.Should().BeNullOrEmpty("degraded tier omits targeting_key");
    }

    [Fact]
    public void TryGetPayload_Drains_AggregatorAfterBuild()
    {
        var agg = new FlagEvaluationAggregator(globalCap: 10, perFlagCap: 5, degradedCap: 5);
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        agg.Add(new FlagEvalEvent("flag-a", "on", "split", "alloc-1", "user-1", t, null));

        FlagEvaluationApi.BuildPayload(agg, service: "svc", env: "prod", version: "1.0");

        // Second call should return null (drained)
        var payload2 = FlagEvaluationApi.BuildPayload(agg, service: "svc", env: "prod", version: "1.0");
        payload2.Should().BeNull("aggregator should be drained after BuildPayload");
    }
}
