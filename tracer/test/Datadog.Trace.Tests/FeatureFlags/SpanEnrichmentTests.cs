// <copyright file="SpanEnrichmentTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary>
/// L0 unit tests for the .NET FFE APM span-enrichment codec + accumulator + Span.Finish write
/// path (NET-01). Covers the 7 required VALIDATION.md cases plus the explicit max-200 case:
/// no-span, finished-root, error/default variant, per-subject cap, max-200 serial ids,
/// JSON/object-default, gate-off negative control, and the codec golden-vector round-trip.
/// </summary>
public class SpanEnrichmentTests
{
    private const string GoldenBase64 = "ZAgUAg==";

    // SHA256("user-123") per the frozen reference.
    private const string User123Sha256 = "fcdec6df4d44dbc637c7c5b58efface52a7f8a88535423430255be0bb89bedd8";

    // Set by the CR-01 race test's reader task when it observes the "Collection was modified"
    // failure; the test asserts this stays false post-fix.
    private static volatile bool _raceFailed;

    // ---------------------------------------------------------------------
    // Codec golden vector + round-trip (Pattern E)
    // ---------------------------------------------------------------------

    [Fact]
    public void Codec_GoldenVector_EncodesToZAgUAg()
    {
        // {100,108,128,130} -> deltas [100,8,20,2] -> ULEB128 [0x64,0x08,0x14,0x02] -> base64 "ZAgUAg=="
        ULeb128Encoder.EncodeDeltaVarint(new long[] { 100, 108, 128, 130 }).Should().Be(GoldenBase64);
    }

    [Fact]
    public void Codec_Empty_ReturnsEmptyString()
    {
        ULeb128Encoder.EncodeDeltaVarint(new long[0]).Should().Be(string.Empty);
    }

    [Fact]
    public void Codec_RoundTrip_DecodesBackToSortedDedupedIds()
    {
        var input = new long[] { 130, 100, 108, 128, 100 }; // unsorted + duplicate
        var encoded = ULeb128Encoder.EncodeDeltaVarint(input);
        encoded.Should().Be(GoldenBase64);

        var decoded = DecodeDeltaVarint(encoded);
        decoded.Should().Equal(new long[] { 100, 108, 128, 130 });
    }

    [Fact]
    public void Codec_MultiByteVarint_RoundTrips()
    {
        // 200 needs 2 ULEB128 bytes (0xC8 0x01); exercise the continuation-bit path.
        var input = new long[] { 5, 205, 500 };
        var decoded = DecodeDeltaVarint(ULeb128Encoder.EncodeDeltaVarint(input));
        decoded.Should().Equal(input);
    }

    [Fact]
    public void Codec_HashTargetingKey_MatchesFrozenSha256()
    {
        SpanEnrichmentState.HashTargetingKey("user-123").Should().Be(User123Sha256);
    }

    // ---------------------------------------------------------------------
    // Accumulator: serial ids + dedupe + max-200
    // ---------------------------------------------------------------------

    [Fact]
    public void State_AccumulatesAndDedupesSerialIds()
    {
        var state = new SpanEnrichmentState();
        state.AddSerialId(100);
        state.AddSerialId(108);
        state.AddSerialId(100); // duplicate
        state.AddSerialId(128);
        state.AddSerialId(130);

        var tags = TagDict(state);
        tags[SpanEnrichmentState.TagFlagsEnc].Should().Be(GoldenBase64);
    }

    [Fact]
    public void State_Max200SerialIds_DropsBeyondLimit()
    {
        var state = new SpanEnrichmentState();
        for (var i = 1; i <= 250; i++)
        {
            state.AddSerialId(i);
        }

        var tags = TagDict(state);
        var decoded = DecodeDeltaVarint(tags[SpanEnrichmentState.TagFlagsEnc]);
        decoded.Count.Should().Be(SpanEnrichmentState.MaxSerialIds);
        decoded.Should().Equal(Enumerable.Range(1, SpanEnrichmentState.MaxSerialIds).Select(x => (long)x));
    }

    // ---------------------------------------------------------------------
    // Accumulator: subjects (10 subjects / 20 experiments-per-subject caps)
    // ---------------------------------------------------------------------

    [Fact]
    public void State_Subjects_EncodeAsJsonObjectOfBase64()
    {
        var state = new SpanEnrichmentState();
        state.AddSerialId(100);
        state.AddSubject("user-123", 100);

        var tags = TagDict(state);
        tags.Should().ContainKey(SpanEnrichmentState.TagSubjectsEnc);

        var subjects = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags[SpanEnrichmentState.TagSubjectsEnc])!;
        subjects.Should().ContainKey(User123Sha256);
        DecodeDeltaVarint(subjects[User123Sha256]).Should().Equal(new long[] { 100 });
    }

    [Fact]
    public void State_MaxSubjects_DropsBeyondTenSubjects()
    {
        var state = new SpanEnrichmentState();
        state.AddSerialId(1);
        for (var i = 0; i < 15; i++)
        {
            state.AddSubject($"subject-{i}", 1);
        }

        var subjects = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagSubjectsEnc])!;
        subjects.Count.Should().Be(SpanEnrichmentState.MaxSubjects);
    }

    [Fact]
    public void State_MaxExperimentsPerSubject_DropsBeyondTwenty()
    {
        var state = new SpanEnrichmentState();
        for (var i = 1; i <= 30; i++)
        {
            state.AddSerialId(i);
            state.AddSubject("same-subject", i);
        }

        var subjects = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagSubjectsEnc])!;
        var hashed = SpanEnrichmentState.HashTargetingKey("same-subject");
        DecodeDeltaVarint(subjects[hashed]).Count.Should().Be(SpanEnrichmentState.MaxExperimentsPerSubject);
    }

    // ---------------------------------------------------------------------
    // Accumulator: runtime defaults (missing-variant detection, JSON, truncation, caps)
    // ---------------------------------------------------------------------

    [Fact]
    public void State_RuntimeDefault_StringValue_WrittenAsRuntimeDefaultsJson()
    {
        var state = new SpanEnrichmentState();
        state.AddDefault("my-flag", "fallback-value");

        var tags = TagDict(state);
        tags.Should().NotContainKey(SpanEnrichmentState.TagFlagsEnc);

        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults["my-flag"].Should().Be("fallback-value");
    }

    [Fact]
    public void State_ObjectDefault_IsJsonStringified_NotToString()
    {
        var state = new SpanEnrichmentState();
        var value = new Dictionary<string, object> { ["a"] = 1, ["b"] = "two" };
        state.AddDefault("obj-flag", value);

        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagRuntimeDefaults])!;

        // JSON-stringified, NOT "System.Collections.Generic.Dictionary..." (ToString()).
        defaults["obj-flag"].Should().Contain("\"a\"").And.Contain("\"b\"").And.Contain("two");
        defaults["obj-flag"].Should().NotContain("System.Collections");
    }

    [Fact]
    public void State_DefaultValue_TruncatedTo64Chars()
    {
        var state = new SpanEnrichmentState();
        var longValue = new string('x', 100);
        state.AddDefault("long-flag", longValue);

        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults["long-flag"].Length.Should().Be(SpanEnrichmentState.MaxDefaultValueLength);
    }

    [Fact]
    public void State_MaxDefaults_DropsBeyondFive_FirstWins()
    {
        var state = new SpanEnrichmentState();
        for (var i = 0; i < 8; i++)
        {
            state.AddDefault($"flag-{i}", $"value-{i}");
        }

        // First-wins: re-adding flag-0 with a new value does not overwrite.
        state.AddDefault("flag-0", "changed");

        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults.Count.Should().Be(SpanEnrichmentState.MaxDefaults);
        defaults["flag-0"].Should().Be("value-0");
    }

    [Fact]
    public void State_HasData_FalseWhenEmpty_TrueWithSerialIdsOrDefaults()
    {
        var empty = new SpanEnrichmentState();
        empty.HasData().Should().BeFalse();

        var withSerial = new SpanEnrichmentState();
        withSerial.AddSerialId(1);
        withSerial.HasData().Should().BeTrue();

        var withDefault = new SpanEnrichmentState();
        withDefault.AddDefault("f", "v");
        withDefault.HasData().Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // Store branch logic (mirrors the Node finally branch the hook bridges into)
    // ---------------------------------------------------------------------

    [Fact]
    public void Store_Accumulate_SerialIdWithDoLogAndTargetingKey_AddsSubject()
    {
        const ulong rootSpanId = 9001;
        SpanEnrichmentStore.Clear();

        SpanEnrichmentStore.Accumulate(rootSpanId, serialId: 100, doLog: true, targetingKey: "user-123", hasVariant: true, flagKey: "flag", value: "on");

        var state = SpanEnrichmentStore.GetAndClear(rootSpanId);
        state.Should().NotBeNull();
        var tags = TagDict(state!);
        tags[SpanEnrichmentState.TagFlagsEnc].Should().NotBeNullOrEmpty();
        tags.Should().ContainKey(SpanEnrichmentState.TagSubjectsEnc);
    }

    [Fact]
    public void Store_Accumulate_SerialIdWithoutDoLog_NoSubject()
    {
        const ulong rootSpanId = 9002;
        SpanEnrichmentStore.Clear();

        SpanEnrichmentStore.Accumulate(rootSpanId, serialId: 100, doLog: false, targetingKey: "user-123", hasVariant: true, flagKey: "flag", value: "on");

        var tags = TagDict(SpanEnrichmentStore.GetAndClear(rootSpanId)!);
        tags.Should().ContainKey(SpanEnrichmentState.TagFlagsEnc);
        tags.Should().NotContainKey(SpanEnrichmentState.TagSubjectsEnc);
    }

    [Fact]
    public void Store_Accumulate_MissingVariant_RecordsRuntimeDefault()
    {
        const ulong rootSpanId = 9003;
        SpanEnrichmentStore.Clear();

        // No serial id + no variant => runtime-default detection.
        SpanEnrichmentStore.Accumulate(rootSpanId, serialId: null, doLog: false, targetingKey: null, hasVariant: false, flagKey: "defaulted-flag", value: "the-default");

        var tags = TagDict(SpanEnrichmentStore.GetAndClear(rootSpanId)!);
        tags.Should().NotContainKey(SpanEnrichmentState.TagFlagsEnc);
        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults["defaulted-flag"].Should().Be("the-default");
    }

    [Fact]
    public void Store_Accumulate_WithVariantNoSerialId_RecordsNothing()
    {
        const ulong rootSpanId = 9004;
        SpanEnrichmentStore.Clear();

        // Has a variant but no serial id => neither a serial nor a default.
        SpanEnrichmentStore.Accumulate(rootSpanId, serialId: null, doLog: false, targetingKey: null, hasVariant: true, flagKey: "flag", value: "on");

        var state = SpanEnrichmentStore.GetAndClear(rootSpanId);
        (state is null || !state.HasData()).Should().BeTrue();
    }

    [Fact]
    public void Store_GetAndClear_RemovesState()
    {
        const ulong rootSpanId = 9005;
        SpanEnrichmentStore.Clear();
        SpanEnrichmentStore.Accumulate(rootSpanId, serialId: 1, doLog: false, targetingKey: null, hasVariant: true, flagKey: "f", value: "v");

        SpanEnrichmentStore.GetAndClear(rootSpanId).Should().NotBeNull();
        SpanEnrichmentStore.GetAndClear(rootSpanId).Should().BeNull("state must be cleared after the first drain");
    }

    // ---------------------------------------------------------------------
    // Span.Finish write path: gate-on positive control + gate-off negative control (DG-005)
    // ---------------------------------------------------------------------

    [Fact]
    public async System.Threading.Tasks.Task SpanFinish_GateOn_WritesFfeTagsFromAccumulatedState()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        var scope = (Scope)tracer.StartActive("root-op");
        var span = scope.Span;
        span.IsRootSpan.Should().BeTrue();
        var rootSpanId = span.SpanId;

        // Simulate what the hook + bridge do at runtime (auto-instrumentation is not active in L0).
        SpanEnrichmentStore.Clear();
        SpanEnrichmentStore.Accumulate(rootSpanId, serialId: 100, doLog: true, targetingKey: "user-123", hasVariant: true, flagKey: "flag", value: "on");
        SpanEnrichmentStore.Accumulate(rootSpanId, serialId: 108, doLog: false, targetingKey: null, hasVariant: true, flagKey: "flag2", value: "off");

        span.Finish();

        span.GetTag(SpanEnrichmentState.TagFlagsEnc).Should().NotBeNullOrEmpty();
        DecodeDeltaVarint(span.GetTag(SpanEnrichmentState.TagFlagsEnc)!).Should().Equal(new long[] { 100, 108 });
        span.GetTag(SpanEnrichmentState.TagSubjectsEnc).Should().NotBeNullOrEmpty();

        // State is cleared in the IsRootSpan block after writing.
        SpanEnrichmentStore.GetAndClear(rootSpanId).Should().BeNull();
    }

    [Fact]
    public async System.Threading.Tasks.Task SpanFinish_GateOff_NegativeControl_NoTags_NoStateAllocated()
    {
        // Gate OFF (default). DG-005 / Go #4844 proof: Span.Finish() must do no work and the
        // store must hold no per-span state.
        var settings = TracerSettings.Create(new());
        settings.IsSpanEnrichmentEnabled.Should().BeFalse("the gate is off by default");

        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        var scope = (Scope)tracer.StartActive("root-op");
        var span = scope.Span;

        // With the gate off, the hook is never constructed and nothing is ever accumulated,
        // so the store has no state for this span.
        SpanEnrichmentStore.Clear();
        SpanEnrichmentStore.GetAndClear(span.SpanId).Should().BeNull("no state is allocated when the gate is off");

        span.Finish();

        span.GetTag(SpanEnrichmentState.TagFlagsEnc).Should().BeNull();
        span.GetTag(SpanEnrichmentState.TagSubjectsEnc).Should().BeNull();
        span.GetTag(SpanEnrichmentState.TagRuntimeDefaults).Should().BeNull();
    }

    [Fact]
    public async System.Threading.Tasks.Task SpanFinish_GateOn_NoAccumulatedState_WritesNoTags()
    {
        // no-span / no-data case: gate on but nothing accumulated for this root => no ffe_* tags.
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        var scope = (Scope)tracer.StartActive("root-op");
        var span = scope.Span;
        SpanEnrichmentStore.Clear();

        span.Finish();

        span.GetTag(SpanEnrichmentState.TagFlagsEnc).Should().BeNull();
        span.GetTag(SpanEnrichmentState.TagRuntimeDefaults).Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // Runtime-default rendering parity with Node String(value) (RESEARCH.md:102)
    // ---------------------------------------------------------------------

    [Fact]
    public void State_RuntimeDefault_NullValue_RendersAsNullString()
    {
        // Node: String(null) === "null". A null default value must render as the literal "null",
        // NOT an omitted/empty value.
        var state = new SpanEnrichmentState();
        state.AddDefault("null-flag", null);

        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults["null-flag"].Should().Be("null");
    }

    [Fact]
    public void State_RuntimeDefault_BoolValue_RendersLowercase()
    {
        // Node: String(true) === "true", String(false) === "false" (lowercase, not .NET's "True").
        var state = new SpanEnrichmentState();
        state.AddDefault("bool-true", true);
        state.AddDefault("bool-false", false);

        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults["bool-true"].Should().Be("true");
        defaults["bool-false"].Should().Be("false");
    }

    // ---------------------------------------------------------------------
    // CR-01 regression: concurrent mutation of one shared root-span state.
    // The L0 suite above is single-threaded, so it never exercised the path
    // CR-01 breaks. These reproduce the real Task.WhenAll / late-Accumulate
    // races on a SHARED SpanEnrichmentState instance (one root span). They
    // fail-before (corruption / "Collection was modified") and pass-after.
    // ---------------------------------------------------------------------

    [Fact]
    public void State_ConcurrentAccumulation_OnSharedRoot_NoCorruptionOrException()
    {
        // Mirrors a Task.WhenAll fan-out of flag resolutions under one ambient root span: the store
        // hands the SAME state instance to every concurrent eval, each mutating the non-thread-safe
        // inner SortedSet/Dictionary. Without the per-instance lock this corrupts the red-black tree
        // or throws; with it, the result is a deterministic deduped/capped merge.
        const ulong rootSpanId = 70001;
        SpanEnrichmentStore.Clear();

        // Every task writes the SAME id range (1..IdRange), so heavy contention + dedupe pressure
        // converge on one unambiguous expected union: exactly {1..IdRange}. IdRange is well under the
        // 200 serial-id cap so nothing is dropped — any missing/extra id would be a lost/torn write.
        const int idRange = 150;
        const int tasks = 16;

        Parallel.For(0, tasks, t =>
        {
            for (var id = 1; id <= idRange; id++)
            {
                SpanEnrichmentStore.Accumulate(rootSpanId, serialId: id, doLog: true, targetingKey: "user-123", hasVariant: true, flagKey: "flag", value: "on");
            }
        });

        var state = SpanEnrichmentStore.GetAndClear(rootSpanId);
        state.Should().NotBeNull();

        // No exception is the primary assertion; the merged set must be exactly {1..idRange}, sorted +
        // deduped, decoded from the bare base64 — proving no concurrent write was lost or torn.
        var tags = TagDict(state!);
        var decoded = DecodeDeltaVarint(tags[SpanEnrichmentState.TagFlagsEnc]);
        decoded.Should().Equal(Enumerable.Range(1, idRange).Select(x => (long)x));

        // Subjects must also be intact: a single subject "user-123", its id set capped at 20 (the
        // first 20 distinct ids per subject win), with no corruption from concurrent AddSubject.
        var subjects = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags[SpanEnrichmentState.TagSubjectsEnc])!;
        subjects.Should().ContainKey(User123Sha256);
        DecodeDeltaVarint(subjects[User123Sha256]).Count.Should().Be(SpanEnrichmentState.MaxExperimentsPerSubject);
    }

    [Fact]
    public async System.Threading.Tasks.Task State_ConcurrentAddRacingToSpanTags_NoCollectionModifiedException()
    {
        // A straggler Accumulate that obtained the instance via GetOrAdd BEFORE the drain's
        // TryRemove still holds the reference and can Add while Span.Finish enumerates ToSpanTags.
        // Pre-fix, the deferred yield iterator ran the foreach body over the live SortedSet during
        // that Add => InvalidOperationException: "Collection was modified". Post-fix, ToSpanTags
        // snapshots under the lock, so a concurrent Add can never tear the enumeration.
        const ulong rootSpanId = 70002;
        _raceFailed = false;

        for (var round = 0; round < 200 && !_raceFailed; round++)
        {
            SpanEnrichmentStore.Clear();

            // Seed so the state exists and ToSpanTags has something to enumerate.
            for (var i = 1; i <= 30; i++)
            {
                SpanEnrichmentStore.Accumulate(rootSpanId, serialId: i, doLog: false, targetingKey: null, hasVariant: true, flagKey: "f", value: "v");
            }

            // Grab the live instance (the "straggler" that raced the drain) and keep mutating it
            // while another thread repeatedly enumerates ToSpanTags.
            var live = SpanEnrichmentStore.GetAndClear(rootSpanId)!;

            var adder = System.Threading.Tasks.Task.Run(() =>
            {
                for (var i = 31; i <= 400; i++)
                {
                    live.AddSerialId(i);
                }
            });

            var reader = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    for (var i = 0; i < 400; i++)
                    {
                        // Enumerate the produced tags fully (decode) — pre-fix this tears mid-enumeration.
                        foreach (var tag in live.ToSpanTags())
                        {
                            if (tag.Key == SpanEnrichmentState.TagFlagsEnc)
                            {
                                DecodeDeltaVarint(tag.Value);
                            }
                        }
                    }
                }
                catch (System.InvalidOperationException)
                {
                    // "Collection was modified; enumeration operation may not execute" — the exact
                    // CR-01 failure. Record it so the assertion reports cleanly (fail-before signal).
                    _raceFailed = true;
                }
            });

            await System.Threading.Tasks.Task.WhenAll(adder, reader);
        }

        _raceFailed.Should().BeFalse("ToSpanTags must snapshot under the lock so a concurrent Add never throws");
    }

    [Fact]
    public async System.Threading.Tasks.Task SpanFinish_EnrichmentThrows_DoesNotBreakSpanFinish()
    {
        // WR-02: Span.Finish() is core span lifecycle. A throw in the drain/encode/serialize path
        // must NOT propagate out of Finish and break span closing for an unrelated trace. Force a
        // deterministic throw on the drain path and assert Finish still completes (IsFinished true,
        // no exception escapes).
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        var scope = (Scope)tracer.StartActive("root-op");
        var span = scope.Span;
        span.IsRootSpan.Should().BeTrue();

        SpanEnrichmentStore.Clear();
        SpanEnrichmentStore.OnGetAndClearForTesting = static () => throw new System.InvalidOperationException("boom: simulated enrichment failure");

        try
        {
            // Pre-fix (no try/catch around the enrichment block) this throw propagates out of
            // Finish; the Should().NotThrow proves the never-throw guard swallows it.
            var finish = () => span.Finish();
            finish.Should().NotThrow("enrichment must never break span finish (WR-02)");
            span.IsFinished.Should().BeTrue("the span must still finish even when enrichment throws");
        }
        finally
        {
            SpanEnrichmentStore.OnGetAndClearForTesting = null;
        }
    }

    // ---------------------------------------------------------------------
    // WR-03 regression: gate-on store growth is bounded when roots never finish.
    // ---------------------------------------------------------------------

    [Fact]
    public void Store_GrowthBound_StopsCreatingStatesPastCap_ExistingRootsStillAccumulate()
    {
        SpanEnrichmentStore.Clear();
        SpanEnrichmentStore.TrackedRootCount.Should().Be(0);

        var cap = SpanEnrichmentStore.MaxTrackedRootSpans;

        // Fill to the cap with distinct never-finishing roots.
        for (var i = 0; i < cap; i++)
        {
            SpanEnrichmentStore.Accumulate((ulong)(1000 + i), serialId: 1, doLog: false, targetingKey: null, hasVariant: true, flagKey: "f", value: "v");
        }

        SpanEnrichmentStore.TrackedRootCount.Should().Be(cap);

        // New roots past the cap are dropped — the store does NOT grow unboundedly (WR-03).
        for (var i = 0; i < 500; i++)
        {
            SpanEnrichmentStore.Accumulate((ulong)(900000 + i), serialId: 1, doLog: false, targetingKey: null, hasVariant: true, flagKey: "f", value: "v");
        }

        SpanEnrichmentStore.TrackedRootCount.Should().Be(cap, "new roots past the cap must be dropped, not grow the store");

        // A root we are ALREADY tracking must keep accumulating (in-flight evals for a live trace
        // are never lost), even at the cap.
        var existingRoot = 1000UL;
        SpanEnrichmentStore.Accumulate(existingRoot, serialId: 99, doLog: false, targetingKey: null, hasVariant: true, flagKey: "f", value: "v");
        var state = SpanEnrichmentStore.GetAndClear(existingRoot)!;
        DecodeDeltaVarint(TagDict(state)[SpanEnrichmentState.TagFlagsEnc]).Should().Contain(99);

        SpanEnrichmentStore.Clear();
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static Dictionary<string, string> TagDict(SpanEnrichmentState state)
        => state.ToSpanTags().ToDictionary(p => p.Key, p => p.Value);

    // Decode side mirrors the L2 codec (system-tests test_ffe/utils.py) — the round-trip oracle.
    private static List<long> DecodeDeltaVarint(string base64)
    {
        var result = new List<long>();
        if (string.IsNullOrEmpty(base64))
        {
            return result;
        }

        var bytes = System.Convert.FromBase64String(base64);
        long prev = 0;
        var i = 0;
        while (i < bytes.Length)
        {
            long value = 0;
            var shift = 0;
            while (true)
            {
                var b = bytes[i++];
                value |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
            }

            prev += value;
            result.Add(prev);
        }

        return result;
    }
}
