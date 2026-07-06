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
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary>
/// Unit tests for the .NET FFE APM span-enrichment codec + accumulator + Span.Finish write
/// path. Covers: the codec golden-vector round-trip, the accumulator caps/dedupe, the accumulate
/// branch logic, and the Span.Finish write path (gate-on positive control, gate-off negative
/// control, and per-trace state isolation).
/// </summary>
[TracerRestorer]
public class SpanEnrichmentTests
{
    private const string GoldenBase64 = "ZAgUAg==";

    // SHA256("user-123") per the frozen reference.
    private const string User123Sha256 = "fcdec6df4d44dbc637c7c5b58efface52a7f8a88535423430255be0bb89bedd8";

    // Set by the concurrency race test's reader task when it observes the "Collection was
    // modified" failure; the test asserts this stays false post-fix.
    private static volatile bool _raceFailed;

    // ---------------------------------------------------------------------
    // Codec golden vector + round-trip
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
    // Accumulate branch logic (serial id -> subject / missing variant -> default)
    // ---------------------------------------------------------------------

    [Fact]
    public void State_Accumulate_SerialIdWithDoLogAndTargetingKey_AddsSubject()
    {
        var state = new SpanEnrichmentState();
        state.Accumulate(serialId: 100, doLog: true, targetingKey: "user-123", hasVariant: true, flagKey: "flag", value: "on");

        var tags = TagDict(state);
        tags[SpanEnrichmentState.TagFlagsEnc].Should().NotBeNullOrEmpty();
        tags.Should().ContainKey(SpanEnrichmentState.TagSubjectsEnc);
    }

    [Fact]
    public void State_Accumulate_SerialIdWithoutDoLog_NoSubject()
    {
        var state = new SpanEnrichmentState();
        state.Accumulate(serialId: 100, doLog: false, targetingKey: "user-123", hasVariant: true, flagKey: "flag", value: "on");

        var tags = TagDict(state);
        tags.Should().ContainKey(SpanEnrichmentState.TagFlagsEnc);
        tags.Should().NotContainKey(SpanEnrichmentState.TagSubjectsEnc);
    }

    [Fact]
    public void State_Accumulate_MissingVariant_RecordsRuntimeDefault()
    {
        var state = new SpanEnrichmentState();

        // No serial id + no variant => runtime-default detection.
        state.Accumulate(serialId: null, doLog: false, targetingKey: null, hasVariant: false, flagKey: "defaulted-flag", value: "the-default");

        var tags = TagDict(state);
        tags.Should().NotContainKey(SpanEnrichmentState.TagFlagsEnc);
        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults["defaulted-flag"].Should().Be("the-default");
    }

    [Fact]
    public void State_Accumulate_WithVariantNoSerialId_RecordsNothing()
    {
        var state = new SpanEnrichmentState();

        // Has a variant but no serial id => neither a serial nor a default.
        state.Accumulate(serialId: null, doLog: false, targetingKey: null, hasVariant: true, flagKey: "flag", value: "on");

        state.HasData().Should().BeFalse();
    }

    [Fact]
    public async Task AccumulateForRoot_NativeEvaluationMetadata_WritesRootTags()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);
        TracerRestorerAttribute.SetTracer(tracer);

        var rootScope = (Scope)tracer.StartActive("root-op", new SpanCreationSettings { FinishOnClose = false });
        var rootSpan = rootScope.Span;
        rootSpan.IsRootSpan.Should().BeTrue();

        try
        {
            using (var childScope = tracer.StartActive("child-op"))
            {
                var evaluation = new Evaluation(
                    "native-flag",
                    "enabled",
                    EvaluationReason.Static,
                    variant: "treatment",
                    metadata: new Dictionary<string, string>
                    {
                        [FeatureFlagsEvaluator.MetadataSplitSerialId] = "100",
                        [FeatureFlagsEvaluator.MetadataDoLog] = "true"
                    });

                // The evaluation happens on the child, but enrichment is keyed to the trace, so it
                // accumulates onto the shared trace context and surfaces on the local root span.
                ((Scope)childScope).Span.Context.TraceContext!.GetOrCreateFeatureFlagEnrichment()
                    .AccumulateForRoot(evaluation, "user-123");
            }
        }
        finally
        {
            rootScope.Dispose();
        }

        rootSpan.Finish();

        DecodeDeltaVarint(rootSpan.GetTag(SpanEnrichmentState.TagFlagsEnc)!).Should().Equal(new long[] { 100 });
        var subjects = JsonConvert.DeserializeObject<Dictionary<string, string>>(rootSpan.GetTag(SpanEnrichmentState.TagSubjectsEnc)!)!;
        subjects.Should().ContainKey(User123Sha256);
        DecodeDeltaVarint(subjects[User123Sha256]).Should().Equal(new long[] { 100 });
    }

    // ---------------------------------------------------------------------
    // Span.Finish write path: gate-on positive control + gate-off negative control
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SpanFinish_GateOn_WritesFfeTagsFromAccumulatedState()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        var scope = (Scope)tracer.StartActive("root-op");
        var span = scope.Span;
        span.IsRootSpan.Should().BeTrue();

        var enrichment = span.Context.TraceContext!.GetOrCreateFeatureFlagEnrichment();
        enrichment.Accumulate(serialId: 100, doLog: true, targetingKey: "user-123", hasVariant: true, flagKey: "flag", value: "on");
        enrichment.Accumulate(serialId: 108, doLog: false, targetingKey: null, hasVariant: true, flagKey: "flag2", value: "off");

        span.Finish();

        span.GetTag(SpanEnrichmentState.TagFlagsEnc).Should().NotBeNullOrEmpty();
        DecodeDeltaVarint(span.GetTag(SpanEnrichmentState.TagFlagsEnc)!).Should().Equal(new long[] { 100, 108 });
        span.GetTag(SpanEnrichmentState.TagSubjectsEnc).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SpanFinish_GateOff_NegativeControl_NoTags_NoStateAllocated()
    {
        var settings = TracerSettings.Create(new());
        settings.IsSpanEnrichmentEnabled.Should().BeFalse("the gate is off by default");

        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        var scope = (Scope)tracer.StartActive("root-op");
        var span = scope.Span;

        // No accumulation happens when the gate is off, so no state is created on the trace context.
        span.Context.TraceContext!.FeatureFlagEnrichment.Should().BeNull("no state is allocated when the gate is off");

        span.Finish();

        span.GetTag(SpanEnrichmentState.TagFlagsEnc).Should().BeNull();
        span.GetTag(SpanEnrichmentState.TagSubjectsEnc).Should().BeNull();
        span.GetTag(SpanEnrichmentState.TagRuntimeDefaults).Should().BeNull();
    }

    [Fact]
    public async Task SpanFinish_GateOn_NoAccumulatedState_WritesNoTags()
    {
        // no-data case: gate on but nothing accumulated for this root => no ffe_* tags and no state.
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        var scope = (Scope)tracer.StartActive("root-op");
        var span = scope.Span;
        span.Context.TraceContext!.FeatureFlagEnrichment.Should().BeNull("state is created lazily on first eval");

        span.Finish();

        span.GetTag(SpanEnrichmentState.TagFlagsEnc).Should().BeNull();
        span.GetTag(SpanEnrichmentState.TagRuntimeDefaults).Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // Runtime-default rendering: null and bool
    // ---------------------------------------------------------------------

    [Fact]
    public void State_RuntimeDefault_NullValue_RendersAsNullString()
    {
        // A null default value must render as the literal "null", NOT an omitted/empty value.
        var state = new SpanEnrichmentState();
        state.AddDefault("null-flag", null);

        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults["null-flag"].Should().Be("null");
    }

    [Fact]
    public void State_RuntimeDefault_BoolValue_RendersLowercase()
    {
        // String(true) === "true", String(false) === "false" (lowercase, not .NET's "True").
        var state = new SpanEnrichmentState();
        state.AddDefault("bool-true", true);
        state.AddDefault("bool-false", false);

        var defaults = JsonConvert.DeserializeObject<Dictionary<string, string>>(TagDict(state)[SpanEnrichmentState.TagRuntimeDefaults])!;
        defaults["bool-true"].Should().Be("true");
        defaults["bool-false"].Should().Be("false");
    }

    // ---------------------------------------------------------------------
    // Concurrency regression: concurrent mutation of one shared trace's state.
    // Reproduces the real Task.WhenAll / late-Accumulate races on a SHARED
    // SpanEnrichmentState instance (one trace). They fail-before (corruption /
    // "Collection was modified") and pass-after.
    // ---------------------------------------------------------------------

    [Fact]
    public void State_ConcurrentAccumulation_OnSharedRoot_NoCorruptionOrException()
    {
        // Mirrors a Task.WhenAll fan-out of flag resolutions under one ambient trace: every concurrent
        // eval mutates the SAME state instance's non-thread-safe inner collections. Without the
        // per-instance lock this corrupts the set or throws; with it, the result is a deterministic
        // deduped/capped merge.
        var state = new SpanEnrichmentState();

        // Every task writes the SAME id range (1..IdRange), so heavy contention + dedupe pressure
        // converge on one unambiguous expected union: exactly {1..IdRange}. IdRange is well under the
        // 200 serial-id cap so nothing is dropped — any missing/extra id would be a lost/torn write.
        const int idRange = 150;
        const int tasks = 16;

        Parallel.For(0, tasks, t =>
        {
            for (var id = 1; id <= idRange; id++)
            {
                state.Accumulate(serialId: id, doLog: true, targetingKey: "user-123", hasVariant: true, flagKey: "flag", value: "on");
            }
        });

        // No exception is the primary assertion; the merged set must be exactly {1..idRange}, sorted +
        // deduped, decoded from the bare base64 — proving no concurrent write was lost or torn.
        var tags = TagDict(state);
        var decoded = DecodeDeltaVarint(tags[SpanEnrichmentState.TagFlagsEnc]);
        decoded.Should().Equal(Enumerable.Range(1, idRange).Select(x => (long)x));

        // Subjects must also be intact: a single subject "user-123", its id set capped at 20 (the
        // first 20 distinct ids per subject win), with no corruption from concurrent AddSubject.
        var subjects = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags[SpanEnrichmentState.TagSubjectsEnc])!;
        subjects.Should().ContainKey(User123Sha256);
        DecodeDeltaVarint(subjects[User123Sha256]).Count.Should().Be(SpanEnrichmentState.MaxExperimentsPerSubject);
    }

    [Fact]
    public async Task State_ConcurrentAddRacingToSpanTags_NoCollectionModifiedException()
    {
        // A straggler Accumulate can Add while Span.Finish enumerates ToSpanTags. Pre-fix, the
        // foreach ran over the live set during that Add => InvalidOperationException: "Collection was
        // modified". Post-fix, ToSpanTags snapshots under the lock, so a concurrent Add can never tear
        // the enumeration.
        _raceFailed = false;

        for (var round = 0; round < 200 && !_raceFailed; round++)
        {
            var live = new SpanEnrichmentState();

            // Seed so the state exists and ToSpanTags has something to enumerate.
            for (var i = 1; i <= 30; i++)
            {
                live.AddSerialId(i);
            }

            var adder = Task.Run(() =>
            {
                for (var i = 31; i <= 400; i++)
                {
                    live.AddSerialId(i);
                }
            });

            var reader = Task.Run(() =>
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
                    // "Collection was modified; enumeration operation may not execute" — the
                    // concurrency failure. Record it so the assertion reports cleanly (fail-before signal).
                    _raceFailed = true;
                }
            });

            await Task.WhenAll(adder, reader);
        }

        _raceFailed.Should().BeFalse("ToSpanTags must snapshot under the lock so a concurrent Add never throws");
    }

    // ---------------------------------------------------------------------
    // Per-trace state: lazy creation, cross-trace isolation, root-only tags
    // ---------------------------------------------------------------------

    [Fact]
    public async Task FeatureFlagEnrichment_LazilyCreated_ReturnsStableInstance()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        using var scope = (Scope)tracer.StartActive("root-op");
        var traceContext = scope.Span.Context.TraceContext!;

        traceContext.FeatureFlagEnrichment.Should().BeNull("no flag has been evaluated yet");

        var created = traceContext.GetOrCreateFeatureFlagEnrichment();
        created.Should().NotBeNull();
        traceContext.FeatureFlagEnrichment.Should().BeSameAs(created, "the created state is now exposed");
        traceContext.GetOrCreateFeatureFlagEnrichment().Should().BeSameAs(created, "subsequent calls reuse the same instance");
    }

    [Fact]
    public async Task SeparateTraces_DoNotCrossContaminate()
    {
        // Turns the reviewer's "spans tagged with flags from a different trace" concern into a passing
        // test: two independent traces accumulate disjoint serial ids, and each root finishes with
        // ONLY its own ids — per-trace state makes cross-contamination structurally impossible.
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        // Starts a root, accumulates, then deactivates the scope (FinishOnClose=false keeps the span
        // alive). The next call therefore starts a brand-new trace, not a child of the first.
        Span StartDetachedRoot(long[] ids)
        {
            using var scope = (Scope)tracer.StartActive("root-op", new SpanCreationSettings { FinishOnClose = false });
            scope.Span.IsRootSpan.Should().BeTrue();
            var enrichment = scope.Span.Context.TraceContext!.GetOrCreateFeatureFlagEnrichment();
            foreach (var id in ids)
            {
                enrichment.Accumulate(serialId: id, doLog: false, targetingKey: null, hasVariant: true, flagKey: "flag", value: "on");
            }

            return scope.Span;
        }

        var idsA = new long[] { 1, 2, 3 };
        var idsB = new long[] { 100, 108, 128 };

        var spanA = StartDetachedRoot(idsA);
        var spanB = StartDetachedRoot(idsB);

        spanA.Context.TraceContext.Should().NotBeSameAs(spanB.Context.TraceContext, "each root owns its own trace context");

        spanA.Finish();
        spanB.Finish();

        DecodeDeltaVarint(spanA.GetTag(SpanEnrichmentState.TagFlagsEnc)!).Should().Equal(idsA);
        DecodeDeltaVarint(spanB.GetTag(SpanEnrichmentState.TagFlagsEnc)!).Should().Equal(idsB);
    }

    [Fact]
    public async Task SpanFinish_TagsLandOnRootOnly_NotChildSpans()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.SpanEnrichmentEnabled, "true" } });
        await using var tracer = TracerHelper.Create(settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object);

        using var rootScope = (Scope)tracer.StartActive("root-op");
        var root = rootScope.Span;

        using (var childScope = (Scope)tracer.StartActive("child-op"))
        {
            var child = childScope.Span;
            child.IsRootSpan.Should().BeFalse();

            // The eval happens on the child, but enrichment is keyed to the shared trace context.
            child.Context.TraceContext!.GetOrCreateFeatureFlagEnrichment()
                .Accumulate(serialId: 100, doLog: false, targetingKey: null, hasVariant: true, flagKey: "flag", value: "on");

            child.Finish();
            child.GetTag(SpanEnrichmentState.TagFlagsEnc).Should().BeNull("a non-root span must never carry ffe tags");
        }

        root.Finish();
        DecodeDeltaVarint(root.GetTag(SpanEnrichmentState.TagFlagsEnc)!).Should().Equal(new long[] { 100 });
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static Dictionary<string, string> TagDict(SpanEnrichmentState state)
        => state.ToSpanTags().ToDictionary(p => p.Key, p => p.Value);

    // Decode side mirrors the cross-SDK codec (system-tests test_ffe/utils.py) — the round-trip oracle.
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
