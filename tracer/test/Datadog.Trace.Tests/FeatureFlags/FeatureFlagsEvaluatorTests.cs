// <copyright file="FeatureFlagsEvaluatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Google.Protobuf.WellKnownTypes;
using Moq;
using Xunit;
using ValueType = Datadog.Trace.FeatureFlags.Rcm.Model.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagsEvaluatorTests
{
#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1500 // Braces for multi-line statements should not share line

    // ---------------------------------------------------------------------
    // MapValue tests
    // ---------------------------------------------------------------------

    public static IEnumerable<object?[]> MapValueCases()
    {
        // targetType, value, expected (or typeof(Exception))
        // String
        yield return new object?[] { "hello", "hello" };
        yield return new object?[] { 123, "123" };
        yield return new object?[] { true, "True" };
        yield return new object?[] { 3.14, "3.14" };
        yield return new object?[] { null, null };

        // Bool
        yield return new object?[] { true, true };
        yield return new object?[] { false, false };
        yield return new object?[] { "true", true };
        yield return new object?[] { "false", false };
        yield return new object?[] { "TRUE", true };
        yield return new object?[] { "FALSE", false };
        yield return new object?[] { 1, true };
        yield return new object?[] { 0, false };
        yield return new object?[] { null, null };

        // Int
        yield return new object?[] { 42, (int)42 };
        yield return new object?[] { "42", (int)42 };
        yield return new object?[] { 3.14, (int)3 };
        yield return new object?[] { "3.14", (int)3 };
        yield return new object?[] { null, null };

        // Double
        yield return new object?[] { 3.14, 3.14 };
        yield return new object?[] { "3.14", 3.14 };
        yield return new object?[] { 42, 42d };
        yield return new object?[] { "42", 42d };
        yield return new object?[] { null, null };

        // Unsupported
        yield return new object?[] { new DateTime(2023, 12, 21), typeof(ArgumentException) };
    }

    [Theory]
    [MemberData(nameof(MapValueCases))]
    public void MapValueTests(object? input, object? expected)
    {
        if (expected is null || expected is string)
        {
            var res = FeatureFlagsEvaluator.MapValue<string>(input);
            Assert.Equal(expected, res);
        }
        else if (expected is int expectedInt)
        {
            var res = FeatureFlagsEvaluator.MapValue<int>(input);
            Assert.Equal(expected, res);
        }
        else if (expected is double expectedDouble)
        {
            var res = FeatureFlagsEvaluator.MapValue<double>(input);
            Assert.Equal(expected, res);
        }
        else if (expected is bool expectedBool)
        {
            var res = FeatureFlagsEvaluator.MapValue<bool>(input);
            Assert.Equal(expected, res);
        }
        else if (expected is System.Type)
        {
            try
            {
                _ = FeatureFlagsEvaluator.MapValue<string>(input);
            }
            catch (Exception res)
            {
                Assert.Equal(expected, res.GetType());
            }
        }
        else
        {
            throw new Exception($"Unknown expected type {expected.GetType()}");
        }
    }

    [Fact]
    public void EvaluateWithoutConfigReturnsProviderNotReadyAndDefault()
    {
        var evaluator = new FeatureFlagsEvaluator(null, null, 1000);
        var ctx = new EvaluationContext("target");

        var result = evaluator.Evaluate("test", 23, ctx);

        Assert.Equal(23, result.Value);
        Assert.Equal(EvaluationReason.ERROR, result.Reason);
        Assert.Equal("PROVIDER_NOT_READY", result.Error);
    }

    [Fact]
    public void EvaluateWithMissingTargetingKeyReturnsTargetingKeyMissing()
    {
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration(), 1000);
        var ctx = new EvaluationContext(string.Empty); // no targetingKey

        var result = evaluator.Evaluate("flag", "default", ctx);

        Assert.Equal("default", result.Value);
        Assert.Equal(EvaluationReason.ERROR, result.Reason);
        Assert.Equal("TARGETING_KEY_MISSING", result.Error);
    }

    [Fact]
    public void EvaluateWithUnknownFlagReturnsFlagNotFound()
    {
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration(), 1000);
        var ctx = new EvaluationContext("user-123");
        var result = evaluator.Evaluate("unknown", "default", ctx);

        Assert.Equal("default", result.Value);
        Assert.Equal(EvaluationReason.ERROR, result.Reason);
        Assert.Equal("FLAG_NOT_FOUND", result.Error);
    }

    [Fact]
    public void EvaluateDisabledFlagReturnsDisabledReason()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["disabled-flag"] = new Flag { Key = "disabled-flag", Enabled = false, VariationType = ValueType.BOOLEAN }
        };
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user");

        var result = evaluator.Evaluate("disabled-flag", true, ctx);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.DISABLED, result.Reason);
        Assert.Null(result.Error);
    }

    [Fact]
    public void EvaluateFlagWithoutAllocationsReturnsGeneralError()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["null-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = ValueType.STRING },
            ["empty-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = ValueType.STRING, Allocations = new List<Allocation>() },
        };
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("allocation");

        var result1 = evaluator.Evaluate("null-allocation", 23, ctx);
        Assert.Equal(23, result1.Value);
        Assert.Equal(EvaluationReason.ERROR, result1.Reason);
        Assert.Equal("GENERAL", result1.Metadata?["errorCode"]);

        var result2 = evaluator.Evaluate("empty-allocation", 23, ctx);
        Assert.Equal(23, result2.Value);
        Assert.Equal(EvaluationReason.ERROR, result2.Reason);
        Assert.Equal("GENERAL", result2.Metadata?["errorCode"]);
    }

    // ---------------------------------------------------------------------
    // FlattenContext tests
    // ---------------------------------------------------------------------

    public static IEnumerable<object[]> FlattenContextCases()
    {
        // empty
        yield return new object[]
        {
                new Dictionary<string, object?>(),
                new Dictionary<string, object?>()
        };

        // scalars
        yield return new object[]
        {
            new Dictionary<string, object?> { { "integer", 1 }, { "double", 23d }, { "boolean", true }, { "string", "string" }, { "null", null } },
            new Dictionary<string, object?> { { "integer", 1 }, { "double", 23d }, { "boolean", true }, { "string", "string" }, { "null", null } },
        };

        // list: [1,2,[4]]
        yield return new object[]
        {
            new Dictionary<string, object?> { { "list", new List<object?> { 1, 2, new List<object?> { 4 } } } },
            new Dictionary<string, object?> { { "list[0]", 1 }, { "list[1]", 2 }, { "list[2][0]", 4 } },
        };

        // nested map
        yield return new object[]
        {
            new Dictionary<string, object?> { { "map", new Dictionary<string, object?> { { "key1", 1 }, { "key2", 2 }, { "key3", new Dictionary<string, object?> { { "key4", 4 } } } } } },
            new Dictionary<string, object?> { { "map.key1", 1 }, { "map.key2", 2 }, { "map.key3.key4", 4 } },
        };
    }

    [Theory]
    [MemberData(nameof(FlattenContextCases))]
    public void FlattenContextFlattensListsAndDictionarys(Dictionary<string, object?> attrs, Dictionary<string, object?> expected)
    {
        var ctx = new EvaluationContext("structure", attrs);
        var flattened = FeatureFlagsEvaluator.FlattenContext(ctx);

        Assert.Equal(expected.Count, flattened.Count);

        foreach (var pair in expected)
        {
            Assert.True(flattened.TryGetValue(pair.Key, out var actual));
            Assert.Equal(pair.Value, actual);
        }
    }

    // ---------------------------------------------------------------------
    // Happy-path evaluation + rule-based + numeric + exposure
    //    These are example slices; you can easily add more tests in same style.
    // ---------------------------------------------------------------------

    [Fact]
    public void EvaluateSimpleStringFlagReturnsTargetingMatch()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["simple-string"] = CreateSimpleFlag("simple-string", ValueType.STRING, "test-value", "on")
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("simple-string", "default", ctx);

        Assert.Equal("test-value", result.Value);
        Assert.Equal(EvaluationReason.TARGETING_MATCH, result.Reason);
        Assert.Equal("on", result.Variant);
    }

    [Fact]
    public void EvaluateRuleBasedFlagMatchesEmailPremium()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["rule-based-flag"] = CreateRuleBasedFlag()
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-premium", new Dictionary<string, object?> { { "email", "john@company.com" } });

        var result = evaluator.Evaluate("rule-based-flag", "default", ctx);

        Assert.Equal("premium", result.Value);
        Assert.Equal(EvaluationReason.TARGETING_MATCH, result.Reason);
        Assert.Equal("premium", result.Variant);
    }

    [Fact]
    public void EvaluateNumericRuleFlagMatchesScoreGte800()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["numeric-rule-flag"] = CreateNumericRuleFlag()
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-vip", new Dictionary<string, object?> { { "score", 850 } });

        var result = evaluator.Evaluate("numeric-rule-flag", "default", ctx);

        Assert.Equal("vip", result.Value);
        Assert.Equal(EvaluationReason.TARGETING_MATCH, result.Reason);
        Assert.Equal("vip", result.Variant);
    }

    [Fact]
    public void EvaluateTimeBasedFlagWithExpiredAllocationReturnsDefaultReason()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["time-based-flag"] = CreateTimeBasedFlag()
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user");

        var result = evaluator.Evaluate("time-based-flag", "default", ctx);

        Assert.Equal("default", result.Value);
        Assert.Equal(EvaluationReason.DEFAULT, result.Reason);
    }

    [Fact]
    public void EvaluateExposureFlagLogsExposureEvent()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["exposure-flag"] = CreateExposureFlag()
        };

        List<Trace.FeatureFlags.Exposure.ExposureEvent> events = new List<Trace.FeatureFlags.Exposure.ExposureEvent>();
        var evaluator = new FeatureFlagsEvaluator((e) => events.Add(e), new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("exposure-flag", "default", ctx);

        Assert.Equal("tracked-value", result.Value);
        Assert.Equal(EvaluationReason.TARGETING_MATCH, result.Reason);
        Assert.Equal("tracked", result.Variant);

        // DoLog=true -> one exposure event
        Assert.Single(events);
    }

    // ---------------------------------------------------------------------
    // Helpers to build flags (minimal subset)
    // ---------------------------------------------------------------------

    private static Flag CreateSimpleFlag(string key, ValueType type, object value, string variantKey)
    {
        var variants = new Dictionary<string, Variant>
        {
            [variantKey] = new Variant { Key = variantKey, Value = (string)value },
        };

        var splits = new List<Split>
        {
            new Split { Shards = new List<Shard>(), VariationKey = variantKey }
        };

        var allocations = new List<Allocation>
        {
            new Allocation { Key = "alloc1", Splits = splits, DoLog = false }
        };

        return new Flag { Key = key, Enabled = true, VariationType = type, Variations = variants, Allocations = allocations };
    }

    private static Flag CreateRuleBasedFlag()
    {
        var variants = new Dictionary<string, Variant>
        {
            ["premium"] = new Variant { Key = "premium", Value = "premium" },
            ["basic"] = new Variant { Key = "basic", Value = "basic" },
        };

        var premiumConditions = new List<ConditionConfiguration>
        {
            new ConditionConfiguration { Operator = ConditionOperator.MATCHES, Attribute = "email", Value = "@company\\.com$" },
        };

        var premiumRules = new List<Rule> { new Rule(premiumConditions) };
        var premiumSplits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "premium" } };
        var premiumAlloc = new Allocation { Key = "premium-alloc", Rules = premiumRules, Splits = premiumSplits, DoLog = false };

        var basicSplits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "basic" } };
        var basicAlloc = new Allocation { Key = "basic-alloc", Splits = basicSplits, DoLog = false };

        return new Flag { Key = "rule-based-flag", Enabled = true, VariationType = ValueType.STRING, Variations = variants, Allocations = new List<Allocation> { premiumAlloc, basicAlloc } };
    }

    private static Flag CreateNumericRuleFlag()
    {
        var variants = new Dictionary<string, Variant>
        {
            ["vip"] = new Variant("vip", "vip"),
            ["regular"] = new Variant("regular", "regular")
        };

        var vipConditions = new List<ConditionConfiguration>
        {
            new ConditionConfiguration { Operator = ConditionOperator.GTE, Attribute = "score", Value = 800 },
        };

        var vipRules = new List<Rule> { new Rule(vipConditions) };
        var vipSplits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "vip" } };
        var vipAlloc = new Allocation { Key = "vip-alloc", Rules = vipRules, Splits = vipSplits, DoLog = false };

        var regularSplits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "regular" } };
        var regularAlloc = new Allocation { Key = "regular-alloc", Splits = regularSplits, DoLog = false };

        return new Flag { Key = "numeric-rule-flag", Enabled = true, VariationType = ValueType.STRING, Variations = variants, Allocations = new List<Allocation> { vipAlloc, regularAlloc } };
    }

    private static Flag CreateTimeBasedFlag()
    {
        var variants = new Dictionary<string, Variant>
        {
            ["time-limited"] = new Variant("time-limited", "time-limited")
        };

        var splits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "time-limited" } };
        var alloc = new Allocation { Key = "time-alloc", StartAt = "2022-01-01T00:00:00Z", EndAt = "2022-12-31T23:59:59Z", Splits = splits, DoLog = false };

        return new Flag { Key = "time-based-flag", Enabled = true, VariationType = ValueType.STRING, Variations = variants, Allocations = new List<Allocation> { alloc } };
    }

    private static Flag CreateExposureFlag()
    {
        var variants = new Dictionary<string, Variant>
        {
            ["tracked"] = new Variant("tracked", "tracked-value")
        };

        var splits = new List<Split> { new Split { VariationKey = "tracked", Shards = new List<Shard>() } };
        var alloc = new Allocation { Key = "exposure-alloc", Splits = splits, DoLog = true };

        return new Flag { Key = "exposure-flag", Enabled = true, VariationType = ValueType.STRING, Variations = variants, Allocations = new List<Allocation> { alloc } };
    }
}
#pragma warning restore SA1204 // Static elements should appear before instance elements
#pragma warning restore SA1500 // Braces for multi-line statements should not share line
