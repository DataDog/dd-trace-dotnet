// <copyright file="FeatureFlagsEvaluatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using Xunit;
using ValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary> FeatureFlagsEvaluator discrete tests </summary>
public partial class FeatureFlagsEvaluatorTests
{
#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1500 // Braces for multi-line statements should not share line

    // ---------------------------------------------------------------------
    // MapValue tests
    // ---------------------------------------------------------------------

    public static IEnumerable<object?[]> MapValueCases()
    {
        // targetType, value, expected, typeof(Exception)
        yield return new object?[] { null, null, null };

        // String
        yield return new object?[] { "hello", "hello", null };
        yield return new object?[] { 123, "123", null };
        yield return new object?[] { true, "True", null };
        yield return new object?[] { 3.14, "3.14", null };

        // Bool
        yield return new object?[] { true, true, null };
        yield return new object?[] { false, false, null };
        yield return new object?[] { "true", true, null };
        yield return new object?[] { "false", false, null };
        yield return new object?[] { "TRUE", true, null };
        yield return new object?[] { "FALSE", false, null };
        yield return new object?[] { 1, true, null };
        yield return new object?[] { 0, false, null };

        // Int
        yield return new object?[] { 42, (int)42, null };
        yield return new object?[] { "42", (int)42, null };

        // Double
        yield return new object?[] { 3.14, 3.14, null };
        yield return new object?[] { "3.14", 3.14, null };
        yield return new object?[] { 42, 42d, null };
        yield return new object?[] { "42", 42d, null };

        // Unsupported
        yield return new object?[] { new DateTime(2023, 12, 21), null, typeof(ArgumentException) };
        yield return new object?[] { "3.14", (int)3, typeof(FormatException) };
        yield return new object?[] { 3.14, (int)3, typeof(FormatException) };
    }

    [Theory]
    [MemberData(nameof(MapValueCases))]
    public void MapValueTests(object? input, object? expected, Type? expectedExceptionType)
    {
        if (expectedExceptionType is not null)
        {
            try
            {
                _ = FeatureFlagsEvaluator.MapValue(Trace.FeatureFlags.ValueType.String, input);
            }
            catch (Exception res)
            {
                Assert.Equal(expectedExceptionType, res.GetType());
            }
        }
        else if (expected is null || expected is string)
        {
            var res = FeatureFlagsEvaluator.MapValue(Trace.FeatureFlags.ValueType.String, input);
            Assert.Equal(expected, res);
        }
        else if (expected is int expectedInt)
        {
            var res = FeatureFlagsEvaluator.MapValue(Trace.FeatureFlags.ValueType.Integer, input);
            Assert.Equal(expected, res);
        }
        else if (expected is double expectedDouble)
        {
            var res = FeatureFlagsEvaluator.MapValue(Trace.FeatureFlags.ValueType.Numeric, input);
            Assert.Equal(expected, res);
        }
        else if (expected is bool expectedBool)
        {
            var res = FeatureFlagsEvaluator.MapValue(Trace.FeatureFlags.ValueType.Boolean, input);
            Assert.Equal(expected, res);
        }
        else
        {
            throw new Exception($"Unknown expected type {expected.GetType()}");
        }
    }

    [Fact]
    public void EvaluateWithoutConfigReturnsProviderNotReadyAndDefault()
    {
        var evaluator = new FeatureFlagsEvaluator(null, null);
        var ctx = new EvaluationContext("target");

        var result = evaluator.Evaluate("test", Trace.FeatureFlags.ValueType.Integer, 23, ctx);

        Assert.Equal(23, result.Value);
        Assert.Equal(EvaluationReason.Error, result.Reason);
        Assert.Equal("PROVIDER_NOT_READY", result.Error);
    }

    [Fact]
    public void EvaluateWithMissingTargetingKeyReturnsTargetingKeyMissing()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["simple-string"] = FeatureFlagsHelpers.CreateSimpleFlag("simple-string", ValueType.String, "default", "on")
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });

        var ctx = new EvaluationContext("user-123");
        var result = evaluator.Evaluate("simple-string", Trace.FeatureFlags.ValueType.String, "default", ctx);
        Assert.Equal("default", result.Value);
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        Assert.Equal("on", result.Variant);

        var noTargettingKeyCtx = new EvaluationContext(string.Empty); // no targetingKey
        result = evaluator.Evaluate("simple-string", Trace.FeatureFlags.ValueType.String, "default", noTargettingKeyCtx);

        Assert.Equal("default", result.Value);
        Assert.Equal(EvaluationReason.Error, result.Reason);
        Assert.Equal("TARGETING_KEY_MISSING", result.Error);
    }

    [Fact]
    public void EvaluateWithUnknownFlagReturnsFlagNotFound()
    {
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration());
        var ctx = new EvaluationContext("user-123");
        var result = evaluator.Evaluate("unknown", Trace.FeatureFlags.ValueType.String, "default", ctx);

        Assert.Equal("default", result.Value);
        Assert.Equal(EvaluationReason.Error, result.Reason);
        Assert.Equal("FLAG_NOT_FOUND", result.Error);
    }

    [Fact]
    public void EvaluateDisabledFlagReturnsDisabledReason()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["disabled-flag"] = new Flag { Key = "disabled-flag", Enabled = false, VariationType = ValueType.Boolean }
        };
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user");

        var result = evaluator.Evaluate("disabled-flag", Trace.FeatureFlags.ValueType.Boolean, true, ctx);

        Assert.Equal(true, result.Value);
        Assert.Equal(EvaluationReason.Disabled, result.Reason);
        Assert.Null(result.Error);
    }

    [Fact]
    public void EvaluateFlagWithTypeMismatchReturnsTypeMismatchError()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["null-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = ValueType.String },
            ["empty-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = ValueType.String, Allocations = new List<Allocation>() },
        };
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("allocation");

        var result1 = evaluator.Evaluate("null-allocation", Trace.FeatureFlags.ValueType.Boolean, 23, ctx);
        Assert.Equal(23, result1.Value);
        Assert.Equal(EvaluationReason.Error, result1.Reason);
        Assert.Equal("TYPE_MISMATCH", result1.FlagMetadata?["errorCode"]);

        var result2 = evaluator.Evaluate("empty-allocation", Trace.FeatureFlags.ValueType.Numeric, 23, ctx);
        Assert.Equal(23, result2.Value);
        Assert.Equal(EvaluationReason.Error, result2.Reason);
        Assert.Equal("TYPE_MISMATCH", result2.FlagMetadata?["errorCode"]);
    }

    [Fact]
    public void EvaluateFlagWithoutAllocationsReturnsDefaultValue()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["null-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = ValueType.String },
            ["empty-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = ValueType.String, Allocations = new List<Allocation>() },
        };
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("allocation");

        var result1 = evaluator.Evaluate("null-allocation", Trace.FeatureFlags.ValueType.String, 23, ctx);
        Assert.Equal(23, result1.Value);
        Assert.Equal(EvaluationReason.Default, result1.Reason);

        var result2 = evaluator.Evaluate("empty-allocation", Trace.FeatureFlags.ValueType.String, 23, ctx);
        Assert.Equal(23, result2.Value);
        Assert.Equal(EvaluationReason.Default, result2.Reason);
    }

    // ---------------------------------------------------------------------
    // FlattenContext tests
    // ---------------------------------------------------------------------

    public static IEnumerable<object?[]> FlattenContextCases()
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
            new Dictionary<string, object?> { { "integer", 1 }, { "list", new List<object?> { 1, 2, new List<object?> { 4 } } } },
            new Dictionary<string, object?> { { "integer", 1 } },
        };

        // nested map
        yield return new object[]
        {
            new Dictionary<string, object?> { { "integer", 1 }, { "map", new Dictionary<string, object?> { { "key1", 1 }, { "key2", 2 }, { "key3", new Dictionary<string, object?> { { "key4", 4 } } } } } },
            new Dictionary<string, object?> { { "integer", 1 } },
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
            ["simple-string"] = FeatureFlagsHelpers.CreateSimpleFlag("simple-string", ValueType.String, "test-value", "on")
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("simple-string", Trace.FeatureFlags.ValueType.String, "default", ctx);

        Assert.Equal("test-value", result.Value);
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        Assert.Equal("on", result.Variant);
    }

    [Fact]
    public void EvaluateRuleBasedFlagMatchesEmailPremium()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["rule-based-flag"] = FeatureFlagsHelpers.CreateRuleBasedFlag()
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-premium", new Dictionary<string, object?> { { "email", "john@company.com" } });

        var result = evaluator.Evaluate("rule-based-flag", Trace.FeatureFlags.ValueType.String, "default", ctx);

        Assert.Equal("premium", result.Value);
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        Assert.Equal("premium", result.Variant);
    }

    [Fact]
    public void EvaluateNumericRuleFlagMatchesScoreGte800()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["numeric-rule-flag"] = FeatureFlagsHelpers.CreateNumericRuleFlag()
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-vip", new Dictionary<string, object?> { { "score", 850 } });

        var result = evaluator.Evaluate("numeric-rule-flag", Trace.FeatureFlags.ValueType.String, "default", ctx);

        Assert.Equal("vip", result.Value);
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        Assert.Equal("vip", result.Variant);
    }

    [Fact]
    public void EvaluateTimeBasedFlagWithExpiredAllocationReturnsDefaultReason()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["time-based-flag"] = FeatureFlagsHelpers.CreateTimeBasedFlag()
        };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user");

        var result = evaluator.Evaluate("time-based-flag", Trace.FeatureFlags.ValueType.String, "default", ctx);

        Assert.Equal("default", result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
    }

    [Fact]
    public void EvaluateExposureFlagLogsExposureEvent()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["exposure-flag"] = FeatureFlagsHelpers.CreateExposureFlag()
        };

        List<Trace.FeatureFlags.Exposure.Model.ExposureEvent> events = new List<Trace.FeatureFlags.Exposure.Model.ExposureEvent>();
        var evaluator = new FeatureFlagsEvaluator((in Trace.FeatureFlags.Exposure.Model.ExposureEvent e) => events.Add(e), new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("exposure-flag", Trace.FeatureFlags.ValueType.String, "default", ctx);

        Assert.Equal("tracked-value", result.Value);
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        Assert.Equal("tracked", result.Variant);

        // DoLog=true -> one exposure event
        Assert.Single(events);
    }
}
#pragma warning restore SA1204 // Static elements should appear before instance elements
#pragma warning restore SA1500 // Braces for multi-line statements should not share line
