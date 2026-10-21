// <copyright file="FlagEvaluationPayloadTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FlagEvaluationPayloadTests
{
    private const long Epoch = 1780000000000;
    private const string TargetCanary = "pii-subject-canary@example.com";
    private const string ContextCanary = "pii-context-canary";
    private const string ErrorCanary = "pii-error-canary";
    private static readonly IReadOnlyDictionary<string, string> ServiceContext = new Dictionary<string, string> { ["service"] = "sdk-test", ["env"] = "test", ["version"] = "1" };

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ExactWireBytesRespectConsentAndTier(bool consent, bool degraded)
    {
        var aggregator = new FlagEvaluationAggregator(degraded ? 0 : 10, 10, 10);
        aggregator.Add(new FlagEvalEvent("flag", "on", "allocation", TargetCanary, Epoch, new Dictionary<string, object?> { ["country"] = ContextCanary }, ErrorCanary, consent));

        var result = Encode(aggregator.Drain());
        var bytes = result.Payloads.Should().ContainSingle().Subject;

        ContainsBytes(bytes, TargetCanary).Should().Be(consent && !degraded);
        ContainsBytes(bytes, ContextCanary).Should().Be(consent && !degraded);
        ContainsBytes(bytes, ErrorCanary).Should().BeFalse();
        var batch = JObject.Parse(Encoding.UTF8.GetString(bytes));
        batch.Properties().Select(p => p.Name).Should().BeEquivalentTo("context", "flagEvaluations");
        batch["context"]!.ToObject<Dictionary<string, string>>().Should().BeEquivalentTo(ServiceContext);
        var row = (JObject)batch["flagEvaluations"]!.Single();
        AssertRequiredShape(row);
        row["error"]!["message"]!.Value<string>().Should().Be("GENERAL");
        row["variant"]!["key"]!.Value<string>().Should().Be("on");
        row["allocation"]!["key"]!.Value<string>().Should().Be("allocation");
        row["runtime_default_used"].Should().BeNull();
        row["targeting_rule"].Should().BeNull();
        row["reason"].Should().BeNull();
        if (degraded)
        {
            row["targeting_key"].Should().BeNull();
            row["context"].Should().BeNull();
        }
        else
        {
            row["targeting_key"]!.Value<string>().Should().Be(FlagEvaluationPrivacy.TargetingKeyForOutput(TargetCanary, consent));
        }

        result.PayloadDroppedEvaluations.Should().Be(0);
        result.SerializationDroppedEvaluations.Should().Be(0);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void SerializerIndependentlyProtectsOverPermissiveEntries(bool keyConsent, bool entryConsent)
    {
        var key = new FullKey(new DegradedKey("flag", "on", null, ErrorCanary), TargetCanary, "ignored", keyConsent);
        var entry = new EvaluationEntry(Epoch, entryConsent, new Dictionary<string, object?> { ["secret"] = ContextCanary });
        var state = new DrainResult(new Dictionary<FullKey, EvaluationEntry> { [key] = entry }, [], 0);

        var bytes = Encode(state).Payloads.Should().ContainSingle().Subject;

        using var scope = new AssertionScope();
        ContainsBytes(bytes, TargetCanary).Should().BeFalse();
        ContainsBytes(bytes, ContextCanary).Should().BeFalse();
        ContainsBytes(bytes, ErrorCanary).Should().BeFalse();
        var row = Rows(bytes).Single();
        row["targeting_key"]!.Value<string>().Should().Be(FlagEvaluationPrivacy.TargetingKeyForOutput(TargetCanary, false));
        row["error"]!["message"]!.Value<string>().Should().Be("GENERAL");
        row["context"].Should().BeNull();
    }

    [Fact]
    public void SerializerAlwaysOmitsDegradedContextEvenWhenEntryIsMalformed()
    {
        var entry = new EvaluationEntry(Epoch, true, new Dictionary<string, object?> { ["secret"] = ContextCanary });
        var state = new DrainResult([], new Dictionary<DegradedKey, EvaluationEntry> { [new("flag", "on", null, ErrorCanary)] = entry }, 0);

        var bytes = Encode(state).Payloads.Should().ContainSingle().Subject;

        ContainsBytes(bytes, ContextCanary).Should().BeFalse();
        ContainsBytes(bytes, ErrorCanary).Should().BeFalse();
        Rows(bytes).Single()["context"].Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SerializerIndependentlyOmitsMalformedTarget(bool consent)
    {
        var key = new FullKey(new("flag", "on", null, null), "\uD800", string.Empty, consent);
        var state = new DrainResult(new Dictionary<FullKey, EvaluationEntry> { [key] = new(Epoch, consent, null) }, [], 0);

        var result = Encode(state);

        Rows(result.Payloads.Single()).Single()["targeting_key"].Should().BeNull();
        result.SerializationDroppedEvaluations.Should().Be(0);
        state.InvalidTargetingKeys.Should().Be(0); // This bypassed admission; output does not double-count.
    }

    [Fact]
    public void SerializerBoundsAndNormalizesInjectedConsentedAttributes()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["nested"] = new Dictionary<string, string> { ["pii"] = ContextCanary },
            ["not-finite"] = double.NaN,
            ["oversized"] = new string('x', 257),
            ["valid"] = true,
        };
        for (var i = 0; i < 300; i++)
        {
            attributes["field-" + i] = i;
        }

        var key = new FullKey(new("flag", "on", null, null), TargetCanary, string.Empty, true);
        var state = new DrainResult(new Dictionary<FullKey, EvaluationEntry> { [key] = new(Epoch, true, attributes) }, [], 0);

        var bytes = Encode(state).Payloads.Single();

        ContainsBytes(bytes, ContextCanary).Should().BeFalse();
        var context = (JObject)Rows(bytes).Single()["context"]!["evaluation"]!;
        context.Properties().Count().Should().BeLessThanOrEqualTo(256);
        context["valid"]!.Value<bool>().Should().BeTrue();
        context["nested"].Should().BeNull();
        context["not-finite"].Should().BeNull();
        context["oversized"].Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("quoted\"\nvariant")]
    public void RuntimeDefaultAndJsonEscapingPreserveVariantSemantics(string? variant)
    {
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(new FlagEvalEvent("flag\"\nkey", variant, null, string.Empty, Epoch, null));

        var row = Rows(Encode(aggregator.Drain()).Payloads.Single()).Single();

        row["flag"]!["key"]!.Value<string>().Should().Be("flag\"\nkey");
        row["targeting_key"]!.Value<string>().Should().BeEmpty();
        row["allocation"].Should().BeNull();
        row["error"].Should().BeNull();
        if (variant is null)
        {
            row["variant"].Should().BeNull();
            row["runtime_default_used"]!.Value<bool>().Should().BeTrue();
        }
        else
        {
            row["variant"]!["key"]!.Value<string>().Should().Be(variant);
            row["runtime_default_used"].Should().BeNull();
        }
    }

    [Fact]
    public void SplitsBatchesUsingEncodedByteSize()
    {
        var single = new FlagEvaluationAggregator(10, 10, 10);
        single.Add(new FlagEvalEvent("one", "on", null, null, Epoch, null));
        var limit = Encode(single.Drain()).Payloads.Single().Length;
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        foreach (var flag in new[] { "one", "two", "six" })
        {
            aggregator.Add(new FlagEvalEvent(flag, "on", null, null, Epoch, null));
        }

        var result = Encode(aggregator.Drain(), limit);

        result.Payloads.Should().HaveCount(3);
        result.Payloads.Should().OnlyContain(p => p.Length <= limit);
        result.Payloads.SelectMany(Rows).Sum(r => r["evaluation_count"]!.Value<long>()).Should().Be(3);
    }

    [Fact]
    public void NonAsciiBytesAreNotMistakenForCharacterCount()
    {
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(new FlagEvalEvent(new string('é', 100), "on", null, null, Epoch, null));
        var state = aggregator.Drain();
        var bytes = Encode(state).Payloads.Single();
        var characterCount = Encoding.UTF8.GetString(bytes).Length;

        var result = Encode(state, characterCount);

        bytes.Length.Should().BeGreaterThan(characterCount);
        result.Payloads.Should().BeEmpty();
        result.PayloadDroppedEvaluations.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedRowDegradesOrDropsWithoutLosingItsNeighbors(bool irreducible)
    {
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(new FlagEvalEvent("before", "on", null, null, Epoch, null));
        for (var i = 0; i < 3; i++)
        {
            aggregator.Add(new FlagEvalEvent(irreducible ? new string('f', 5000) : "large", "on", null, new string('t', 5000), Epoch, null, observeFullEvaluationData: true));
        }

        aggregator.Add(new FlagEvalEvent("after", "on", null, null, Epoch, null));

        var result = Encode(aggregator.Drain(), 512);
        var rows = result.Payloads.SelectMany(Rows).ToList();

        result.Payloads.Should().OnlyContain(p => p.Length <= 512);
        rows.Select(r => r["flag"]!["key"]!.Value<string>()).Should().Contain("before").And.Contain("after");
        rows.Sum(r => r["evaluation_count"]!.Value<long>()).Should().Be(irreducible ? 2 : 5);
        result.PayloadDegradedEvaluations.Should().Be(irreducible ? 0 : 3);
        result.PayloadDroppedEvaluations.Should().Be(irreducible ? 3 : 0);
        result.SerializationDroppedEvaluations.Should().Be(0);
        if (!irreducible)
        {
            rows.Single(r => r["flag"]!["key"]!.Value<string>() == "large")["targeting_key"].Should().BeNull();
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [InlineData("flag", false)]
    [InlineData("variant", false)]
    [InlineData("allocation", false)]
    [InlineData("flag", true)]
    [InlineData("target", false)]
    public void ObviouslyOversizedKeysDoNotAllocateTheirSerializedRepresentation(string dimension, bool degraded)
    {
        var large = new string('x', 1024 * 1024);
        var dimensions = new DegradedKey(dimension == "flag" ? large : "flag", dimension == "variant" ? large : "on", dimension == "allocation" ? large : null, null);
        var entry = new EvaluationEntry(Epoch, true, null);
        entry.Observe(Epoch, true);
        var state = degraded
                        ? new DrainResult([], new Dictionary<DegradedKey, EvaluationEntry> { [dimensions] = entry }, 0)
                        : new DrainResult(new Dictionary<FullKey, EvaluationEntry> { [new(dimensions, dimension == "target" ? large : "subject", string.Empty, true)] = entry }, [], 0);
        var warmup = new DrainResult(new Dictionary<FullKey, EvaluationEntry> { [new(new("flag", "on", null, null), "subject", string.Empty, true)] = new(Epoch, true, null) }, [], 0);
        Encode(warmup, 512);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = Encode(state, 512);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Inputs and aggregation are outside this measurement. Encoding a row that can
        // never fit must not create megabyte-sized JSON strings or UTF-8 arrays first.
        allocated.Should().BeLessThan(64 * 1024);
        result.SerializationDroppedEvaluations.Should().Be(0);
        result.PayloadDroppedEvaluations.Should().Be(dimension == "target" ? 0 : 2);
        result.PayloadDegradedEvaluations.Should().Be(dimension == "target" ? 2 : 0);
        if (dimension == "target")
        {
            var row = Rows(result.Payloads.Single()).Single();
            row["evaluation_count"]!.Value<long>().Should().Be(2);
            row["targeting_key"].Should().BeNull();
            row["context"].Should().BeNull();
        }
        else
        {
            result.Payloads.Should().BeEmpty();
        }
    }
#endif

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    public void OversizedTargetPreservesPrivacyAndMalformedKeySemantics(bool keyConsent, bool entryConsent, bool valid)
    {
        var target = (valid ? string.Empty : "\uD800") + new string('x', 4096);
        var entry = new EvaluationEntry(Epoch, entryConsent, new Dictionary<string, object?> { ["country"] = "US" });
        var key = new FullKey(new("flag", "on", null, null), target, string.Empty, keyConsent);
        var state = new DrainResult(new Dictionary<FullKey, EvaluationEntry> { [key] = entry }, [], 0);

        var result = Encode(state, 512);
        var row = Rows(result.Payloads.Single()).Single();

        result.PayloadDroppedEvaluations.Should().Be(0);
        result.SerializationDroppedEvaluations.Should().Be(0);
        if (keyConsent && entryConsent && valid)
        {
            result.PayloadDegradedEvaluations.Should().Be(1);
            row["targeting_key"].Should().BeNull();
            row["context"].Should().BeNull();
        }
        else
        {
            result.PayloadDegradedEvaluations.Should().Be(0);
            if (valid)
            {
                row["targeting_key"]!.Value<string>().Should().StartWith("sha256_").And.HaveLength(71);
                row["context"].Should().BeNull();
            }
            else
            {
                row["targeting_key"].Should().BeNull();
                row["context"]!["evaluation"]!["country"]!.Value<string>().Should().Be("US");
            }
        }
    }

    [Fact]
    public void MalformedRowDoesNotPoisonOtherRows()
    {
        var good = new FullKey(new("good", "on", null, null), null, string.Empty, false);
        var bad = new FullKey(new(null!, "on", null, null), null, string.Empty, false);
        var badEntry = new EvaluationEntry(Epoch, false, null);
        badEntry.Observe(Epoch, false);
        var state = new DrainResult(new Dictionary<FullKey, EvaluationEntry> { [bad] = badEntry, [good] = new(Epoch, false, null) }, [], 0);

        var result = Encode(state);

        Rows(result.Payloads.Single()).Single()["flag"]!["key"]!.Value<string>().Should().Be("good");
        result.SerializationDroppedEvaluations.Should().Be(2);
        result.PayloadDroppedEvaluations.Should().Be(0);
    }

    [Fact]
    public void EmptyDrainDoesNotEmitEmptyPayload()
    {
        Encode(new DrainResult([], [], 4)).Payloads.Should().BeEmpty();
    }

    private static FlagEvaluationPayloadResult Encode(DrainResult state, int limit = 5 * 1024 * 1024) => FlagEvaluationPayload.Encode(state, ServiceContext, Epoch + 100, limit);

    private static IEnumerable<JObject> Rows(byte[] bytes) => JObject.Parse(Encoding.UTF8.GetString(bytes))["flagEvaluations"]!.Children<JObject>();

    private static bool ContainsBytes(byte[] bytes, string text)
    {
        var needle = Encoding.UTF8.GetBytes(text);
        for (var start = 0; start <= bytes.Length - needle.Length; start++)
        {
            var offset = 0;
            while (offset < needle.Length && bytes[start + offset] == needle[offset])
            {
                offset++;
            }

            if (offset == needle.Length)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertRequiredShape(JObject row)
    {
        // From flageval-worker batchedflagevaluations.json at dd-source 3a0aba914679.
        row.Properties().Select(p => p.Name).Should().OnlyContain(name => new[]
        {
            "timestamp", "flag", "first_evaluation", "last_evaluation", "evaluation_count",
            "variant", "allocation", "runtime_default_used", "targeting_key", "context", "error",
        }.Contains(name));
        row["timestamp"]!.Type.Should().Be(JTokenType.Integer);
        row["timestamp"]!.Value<long>().Should().Be(Epoch + 100);
        row["first_evaluation"]!.Value<long>().Should().Be(Epoch);
        row["last_evaluation"]!.Value<long>().Should().Be(Epoch);
        row["evaluation_count"]!.Value<long>().Should().Be(1);
        row["flag"]!.Type.Should().Be(JTokenType.Object);
        row["flag"]!["key"]!.Value<string>().Should().Be("flag");
    }
}
