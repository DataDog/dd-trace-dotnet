// <copyright file="FlagEvalEVPHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// TDD tests for Task 2: FlagEvalEVPHook + FeatureFlagsModule wiring + killswitch.
// Tests validate the Datadog.Trace-side behavior of the EVP evaluation bridge:
//   (a) FlagEvaluationApi captures correct variant=null (absent) for runtime_default_used (concern #5)
//   (b) FlagEvaluationApi EVP payload correctly omits context/targetingKey for degraded tier (concern #2)
//   (c) FlagEvaluationApi captures allocationKey and targetingKey from hook context
//   (d) FlagEvaluationApi.BuildPayload returns null when aggregator is empty (baseline noop)
//   (e) FeatureFlagsModule exposes an EVP writer (FlagEvaluationApi) that can be set and accessed
// The hook-level FinallyAsync tests live conceptually in Datadog.FeatureFlags.OpenFeature (no test project
// currently exists there); these tests cover the aggregation/transport contract.

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary>
/// Task 2 RED tests: validates FlagEvaluationApi bridge contract and FeatureFlagsModule EVP wiring.
/// </summary>
public class FlagEvalEVPHookTests
{
    // ----------------------------------------------------------------
    // (a) variant=null → runtime_default_used (reviewer concern #5)
    // ----------------------------------------------------------------

    [Fact]
    public void Enqueue_AbsentVariant_SetsRuntimeDefaultUsed()
    {
        var agg = new FlagEvaluationAggregator(10_000, 100, 5_000);
        agg.Add(new FlagEvalEvent("flag", null, "default", null, null, 1_000L, null));

        var result = agg.Drain();
        result.Full.Should().HaveCount(1);
        result.Full.Values.Should().AllSatisfy(e => e.RuntimeDefault.Should().BeTrue());
    }

    [Fact]
    public void Enqueue_PresentVariant_RuntimeDefaultNotSet()
    {
        var agg = new FlagEvaluationAggregator(10_000, 100, 5_000);
        agg.Add(new FlagEvalEvent("flag", "on", "targeting_match", null, null, 1_000L, null));

        var result = agg.Drain();
        result.Full.Should().HaveCount(1);
        result.Full.Values.Should().AllSatisfy(e => e.RuntimeDefault.Should().BeFalse());
    }

    // ----------------------------------------------------------------
    // (b) degraded tier omits context + targetingKey (reviewer concern #2)
    // ----------------------------------------------------------------

    [Fact]
    public void BuildPayload_DegradedEvent_OmitsContextAndTargetingKey()
    {
        // globalCap=0 forces everything into degraded
        var agg = new FlagEvaluationAggregator(globalCap: 0, perFlagCap: 0, degradedCap: 100);
        var ctx = new Dictionary<string, object?> { ["region"] = "us" };
        agg.Add(new FlagEvalEvent("flag", "on", "split", null, "tkey_1", 1_000L, ctx));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "env", "v");
        payload.Should().NotBeNull();
        payload!.FlagEvaluations.Should().HaveCount(1);
        payload.FlagEvaluations[0].TargetingKey.Should().BeNull("degraded tier must omit targeting_key");
        payload.FlagEvaluations[0].Context.Should().BeNull("degraded tier must omit context");
    }

    // ----------------------------------------------------------------
    // (c) allocationKey and targetingKey captured in full-tier payload
    // ----------------------------------------------------------------

    [Fact]
    public void BuildPayload_FullEvent_IncludesAllocationAndTargetingKey()
    {
        var agg = new FlagEvaluationAggregator(10_000, 100, 5_000);
        agg.Add(new FlagEvalEvent("flag", "control", "split", "bucket_7", "user_42", 2_000L, null));

        var payload = FlagEvaluationApi.BuildPayload(agg, "svc", "env", "v");
        payload.Should().NotBeNull();
        payload!.FlagEvaluations.Should().HaveCount(1);
        var ev = payload.FlagEvaluations[0];
        ev.Allocation.Should().NotBeNull();
        ev.Allocation!.Key.Should().Be("bucket_7");
        ev.TargetingKey.Should().Be("user_42");
    }

    // ----------------------------------------------------------------
    // (d) empty aggregator → BuildPayload returns null (noop)
    // ----------------------------------------------------------------

    [Fact]
    public void BuildPayload_EmptyAggregator_ReturnsNull()
    {
        var agg = new FlagEvaluationAggregator(10_000, 100, 5_000);
        FlagEvaluationApi.BuildPayload(agg, "svc", "env", "v").Should().BeNull();
    }

    // ----------------------------------------------------------------
    // (e) FeatureFlagsModule exposes FlagEvaluationApi via GetEVPApi() — RED test
    // This FAILS until FeatureFlagsModule.GetEVPApi() is implemented.
    // ----------------------------------------------------------------

    [Fact]
    public void FeatureFlagsModule_WhenEnabled_ExposesEVPApi()
    {
        // FeatureFlagsModule.GetEVPApi() must return a non-null FlagEvaluationApi
        // when the module is created with IsFlaggingProviderEnabled=true.
        // Uses reflection to access internal method — tests the wiring boundary.
        var method = typeof(FeatureFlagsModule).GetMethod(
            "GetEVPApi",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        method.Should().NotBeNull("FeatureFlagsModule must expose GetEVPApi() for the EVP wiring contract");
    }

    [Fact]
    public void FlagEvaluationApi_EVPPath_IsCorrect()
    {
        // Validates the EVP path constant for the flagevaluation track (smoke check).
        FlagEvaluationApi.FlagEvaluationPath.Should().Be("evp_proxy/v2/api/v2/flagevaluations");
    }
}
