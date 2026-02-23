// <copyright file="FeatureFlagsEvaluatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary> FeatureFlagsEvaluator discrete tests </summary>
public partial class FeatureFlagsEvaluatorTests
{
#pragma warning disable SA1201 // A method should not follow a class
#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1500 // Braces for multi-line statements should not share line

    // ---------------------------------------------------------------------
    // MapValue tests
    // ---------------------------------------------------------------------

    public class MapValueTestCase : IXunitSerializable
    {
        public MapValueTestCase()
        {
        }

        public MapValueTestCase(object? input, object? expected, Type? expectedExceptionType)
        {
            Input = input;
            Expected = expected;
            ExpectedExceptionType = expectedExceptionType;
        }

        public object? Input { get; private set; }

        public object? Expected { get; private set; }

        public Type? ExpectedExceptionType { get; private set; }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("InputType", Input?.GetType().Name ?? "null");
            info.AddValue("InputValue", Input?.ToString());
            info.AddValue("ExpectedType", Expected?.GetType().Name ?? "null");
            info.AddValue("ExpectedValue", Expected?.ToString());
            info.AddValue("ExceptionType", ExpectedExceptionType?.AssemblyQualifiedName);
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            var inputType = info.GetValue<string>("InputType");
            var inputValue = info.GetValue<string>("InputValue");
            Input = Decode(inputType, inputValue);

            var expectedType = info.GetValue<string>("ExpectedType");
            var expectedValue = info.GetValue<string>("ExpectedValue");
            Expected = Decode(expectedType, expectedValue);

            var exceptionTypeName = info.GetValue<string>("ExceptionType");
            ExpectedExceptionType = exceptionTypeName is null ? null : Type.GetType(exceptionTypeName);
        }

        private static object? Decode(string typeName, string? value) => typeName switch
        {
            "null" => null,
            "String" => value,
            "Int32" => int.Parse(value!, CultureInfo.InvariantCulture),
            "Double" => double.Parse(value!, CultureInfo.InvariantCulture),
            "Boolean" => bool.Parse(value!),
            "DateTime" => DateTime.Parse(value!, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unknown type: {typeName}")
        };
    }

    public static TheoryData<MapValueTestCase> MapValueCases()
        => new()
        {
            // input, expected, expectedExceptionType
            new(null, null, null),

            // String
            new("hello", "hello", null),
            new(123, "123", null),
            new(true, "True", null),
            new(3.14, "3.14", null),

            // Bool
            new(true, true, null),
            new(false, false, null),
            new("true", true, null),
            new("false", false, null),
            new("TRUE", true, null),
            new("FALSE", false, null),
            new(1, true, null),
            new(0, false, null),

            // Int
            new(42, 42, null),
            new("42", 42, null),

            // Double
            new(3.14, 3.14, null),
            new("3.14", 3.14, null),
            new(42, 42d, null),
            new("42", 42d, null),

            // Unsupported
            new(new DateTime(2023, 12, 21), null, typeof(ArgumentException)),
            new("3.14", 3, typeof(FormatException)),
            new(3.14, 3, typeof(FormatException)),
        };

    [Theory]
    [MemberData(nameof(MapValueCases))]
    public void MapValueTests(MapValueTestCase tc)
    {
        var input = tc.Input;
        var expected = tc.Expected;
        var expectedExceptionType = tc.ExpectedExceptionType;

        if (expectedExceptionType is not null)
        {
            try
            {
                _ = FeatureFlagsEvaluator.MapValue(Datadog.Trace.FeatureFlags.ValueType.String, input);
            }
            catch (Exception res)
            {
                Assert.Equal(expectedExceptionType, res.GetType());
            }
        }
        else if (expected is null || expected is string)
        {
            var res = FeatureFlagsEvaluator.MapValue(Datadog.Trace.FeatureFlags.ValueType.String, input);
            res.Should().Be(expected);
        }
        else if (expected is int)
        {
            var res = FeatureFlagsEvaluator.MapValue(Datadog.Trace.FeatureFlags.ValueType.Integer, input);
            res.Should().Be(expected);
        }
        else if (expected is double)
        {
            var res = FeatureFlagsEvaluator.MapValue(Datadog.Trace.FeatureFlags.ValueType.Numeric, input);
            res.Should().Be(expected);
        }
        else if (expected is bool)
        {
            var res = FeatureFlagsEvaluator.MapValue(Datadog.Trace.FeatureFlags.ValueType.Boolean, input);
            res.Should().Be(expected);
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

        var result = evaluator.Evaluate("test", Datadog.Trace.FeatureFlags.ValueType.Integer, 23, ctx);

        result.Value.Should().Be(23);
        Assert.Equal(EvaluationReason.Error, result.Reason);
        result.Error.Should().Be("PROVIDER_NOT_READY");
    }

    [Fact]
    public void EvaluateWithMissingTargetingKeyReturnsTargetingKeyMissing()
    {
        var flags = new Dictionary<string, Flag>
                    {
                        ["simple-string"] = FeatureFlagsHelpers.CreateSimpleFlag("simple-string", Datadog.Trace.FeatureFlags.ValueType.String, "default", "on")
                    };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });

        var ctx = new EvaluationContext("user-123");
        var result = evaluator.Evaluate("simple-string", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);
        result.Value.Should().Be("default");
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        result.Variant.Should().Be("on");

        var noTargettingKeyCtx = new EvaluationContext(string.Empty); // no targetingKey
        result = evaluator.Evaluate("simple-string", Datadog.Trace.FeatureFlags.ValueType.String, "default", noTargettingKeyCtx);

        result.Value.Should().Be("default");
        Assert.Equal(EvaluationReason.Error, result.Reason);
        result.Error.Should().Be("TARGETING_KEY_MISSING");
    }

    [Fact]
    public void EvaluateWithUnknownFlagReturnsFlagNotFound()
    {
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration());
        var ctx = new EvaluationContext("user-123");
        var result = evaluator.Evaluate("unknown", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        result.Value.Should().Be("default");
        Assert.Equal(EvaluationReason.Error, result.Reason);
        result.Error.Should().Be("FLAG_NOT_FOUND");
    }

    [Fact]
    public void EvaluateDisabledFlagReturnsDisabledReason()
    {
        var flags = new Dictionary<string, Flag>
                    {
                        ["disabled-flag"] = new Flag { Key = "disabled-flag", Enabled = false, VariationType = Datadog.Trace.FeatureFlags.ValueType.Boolean }
                    };
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user");

        var result = evaluator.Evaluate("disabled-flag", Datadog.Trace.FeatureFlags.ValueType.Boolean, true, ctx);

        result.Value.Should().Be(true);
        Assert.Equal(EvaluationReason.Disabled, result.Reason);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void EvaluateFlagWithTypeMismatchReturnsTypeMismatchError()
    {
        var flags = new Dictionary<string, Flag>
                    {
                        ["null-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = Datadog.Trace.FeatureFlags.ValueType.String },
                        ["empty-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = Datadog.Trace.FeatureFlags.ValueType.String, Allocations = [] },
                    };
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("allocation");

        var result1 = evaluator.Evaluate("null-allocation", Datadog.Trace.FeatureFlags.ValueType.Boolean, 23, ctx);
        result1.Value.Should().Be(23);
        Assert.Equal(EvaluationReason.Error, result1.Reason);
        (result1.FlagMetadata?["errorCode"]).Should().Be("TYPE_MISMATCH");

        var result2 = evaluator.Evaluate("empty-allocation", Datadog.Trace.FeatureFlags.ValueType.Numeric, 23, ctx);
        result2.Value.Should().Be(23);
        Assert.Equal(EvaluationReason.Error, result2.Reason);
        (result2.FlagMetadata?["errorCode"]).Should().Be("TYPE_MISMATCH");
    }

    [Fact]
    public void EvaluateFlagWithoutAllocationsReturnsDefaultValue()
    {
        var flags = new Dictionary<string, Flag>
                    {
                        ["null-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = Datadog.Trace.FeatureFlags.ValueType.String },
                        ["empty-allocation"] = new Flag { Key = "target", Enabled = true, VariationType = Datadog.Trace.FeatureFlags.ValueType.String, Allocations = [] },
                    };
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("allocation");

        var result1 = evaluator.Evaluate("null-allocation", Datadog.Trace.FeatureFlags.ValueType.String, 23, ctx);
        result1.Value.Should().Be(23);
        Assert.Equal(EvaluationReason.Default, result1.Reason);

        var result2 = evaluator.Evaluate("empty-allocation", Datadog.Trace.FeatureFlags.ValueType.String, 23, ctx);
        result2.Value.Should().Be(23);
        Assert.Equal(EvaluationReason.Default, result2.Reason);
    }

    // ---------------------------------------------------------------------
    // FlattenContext tests
    // ---------------------------------------------------------------------

    [Fact]
    public void FlattenContextWithEmptyDictionary()
    {
        var attrs = new Dictionary<string, object?>();
        var expected = new Dictionary<string, object?>();
        AssertFlattenContext(attrs, expected);
    }

    [Fact]
    public void FlattenContextWithScalars()
    {
        var attrs = new Dictionary<string, object?> { { "integer", 1 }, { "double", 23d }, { "boolean", true }, { "string", "string" }, { "null", null } };
        var expected = new Dictionary<string, object?> { { "integer", 1 }, { "double", 23d }, { "boolean", true }, { "string", "string" }, { "null", null } };
        AssertFlattenContext(attrs, expected);
    }

    [Fact]
    public void FlattenContextStripsNestedLists()
    {
        // list: [1,2,[4]] — lists are stripped
        var attrs = new Dictionary<string, object?> { { "integer", 1 }, { "list", new List<object?> { 1, 2, new List<object?> { 4 } } } };
        var expected = new Dictionary<string, object?> { { "integer", 1 } };
        AssertFlattenContext(attrs, expected);
    }

    [Fact]
    public void FlattenContextStripsNestedMaps()
    {
        var attrs = new Dictionary<string, object?> { { "integer", 1 }, { "map", new Dictionary<string, object?> { { "key1", 1 }, { "key2", 2 }, { "key3", new Dictionary<string, object?> { { "key4", 4 } } } } } };
        var expected = new Dictionary<string, object?> { { "integer", 1 } };
        AssertFlattenContext(attrs, expected);
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
                        ["simple-string"] = FeatureFlagsHelpers.CreateSimpleFlag("simple-string", Datadog.Trace.FeatureFlags.ValueType.String, "test-value", "on")
                    };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("simple-string", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        result.Value.Should().Be("test-value");
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        result.Variant.Should().Be("on");
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

        var result = evaluator.Evaluate("rule-based-flag", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        result.Value.Should().Be("premium");
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        result.Variant.Should().Be("premium");
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

        var result = evaluator.Evaluate("numeric-rule-flag", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        result.Value.Should().Be("vip");
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        result.Variant.Should().Be("vip");
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

        var result = evaluator.Evaluate("time-based-flag", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        result.Value.Should().Be("default");
        Assert.Equal(EvaluationReason.Default, result.Reason);
    }

    [Fact]
    public void EvaluateExposureFlagLogsExposureEvent()
    {
        var flags = new Dictionary<string, Flag>
                    {
                        ["exposure-flag"] = FeatureFlagsHelpers.CreateExposureFlag()
                    };

        List<Trace.FeatureFlags.Exposure.Model.ExposureEvent> events = [];
        var evaluator = new FeatureFlagsEvaluator((in e) => events.Add(e), new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("exposure-flag", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        result.Value.Should().Be("tracked-value");
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        result.Variant.Should().Be("tracked");

        // DoLog=true -> one exposure event
        Assert.Single(events);
    }

    // ---------------------------------------------------------------------
    // ISO 8601 Date Parsing tests
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("2020-01-01T00:00:00.123Z", "2099-12-31T23:59:59.456Z")]             // 3-digit milliseconds
    [InlineData("2020-01-01T00:00:00.123456Z", "2099-12-31T23:59:59.654321Z")]       // 6-digit microseconds
    [InlineData("2020-01-01T00:00:00.123456789Z", "2099-12-31T23:59:59.987654321Z")] // 9-digit nanoseconds (last 2 digits truncated by .NET)
    [InlineData("2020-01-01T00:00:00Z", "2099-12-31T23:59:59Z")]                     // no fractional seconds
    [InlineData("2020-01-01T00:00:00.1Z", "2099-12-31T23:59:59.9Z")]                 // 1-digit
    [InlineData("2020-01-01T00:00:00.12Z", "2099-12-31T23:59:59.99Z")]               // 2-digit
    public void EvaluateTimeBasedFlagWithVariousIso8601DateFormats(string startAt, string endAt)
    {
        var flag = CreateTimeBasedFlagWithDates("iso8601-flag", startAt, endAt);
        var flags = new Dictionary<string, Flag> { ["iso8601-flag"] = flag };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("iso8601-flag", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        // The allocation is active (2020-2099 dates), so it should match
        result.Value.Should().Be("time-limited");
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        result.Variant.Should().Be("time-limited");
    }

    [Theory]
    [InlineData("2020-01-01T00:00:00.235982Z", "2020-12-31T23:59:59.235982Z")]       // 6-digit microseconds (past)
    [InlineData("2020-01-01T00:00:00Z", "2020-12-31T23:59:59Z")]                     // no fractional seconds (past)
    [InlineData("2020-01-01T00:00:00.123456789Z", "2020-12-31T23:59:59.987654321Z")] // 9-digit nanoseconds (truncated to 7 digits by .NET, but parses correctly)
    public void EvaluateTimeBasedFlagWithExpiredMicrosecondDatesReturnsDefault(string startAt, string endAt)
    {
        var flag = CreateTimeBasedFlagWithDates("expired-flag", startAt, endAt);
        var flags = new Dictionary<string, Flag> { ["expired-flag"] = flag };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("expired-flag", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        // The allocation is expired (2020 dates), so it should return default
        result.Value.Should().Be("default");
        Assert.Equal(EvaluationReason.Default, result.Reason);
    }

    [Theory]
    [InlineData("1/1/2020", "12/31/2099")]                         // US short date format
    [InlineData("2020/01/01T00:00:00Z", "2099/12/31T23:59:59Z")]   // slash separators
    [InlineData("01 Jan 2020 00:00:00Z", "31 Dec 2099 23:59:59Z")] // RFC 2822 style
    public void EvaluateTimeBasedFlagWithNonStandardDateFormatsStillParses(string startAt, string endAt)
    {
        // TryParse accepts various date formats beyond strict RFC 3339.
        // This test documents this behavior - since dates come from our controlled backend,
        // accepting broader formats is acceptable.
        var flag = CreateTimeBasedFlagWithDates("non-standard-flag", startAt, endAt);
        var flags = new Dictionary<string, Flag> { ["non-standard-flag"] = flag };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("non-standard-flag", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        // The allocation is active (2020-2099 dates), so it should match
        result.Value.Should().Be("time-limited");
        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        result.Variant.Should().Be("time-limited");
    }

    [Theory]
    [InlineData("not-a-date", "2099-12-31T23:59:59Z")]           // invalid startAt
    [InlineData("2020-01-01T00:00:00Z", "not-a-date")]           // invalid endAt
    [InlineData("garbage-123-xyz", "2099-12-31T23:59:59Z")]      // garbage string
    [InlineData("", "2099-12-31T23:59:59Z")]                     // empty string
    [InlineData("abc123", "2099-12-31T23:59:59Z")]               // alphanumeric garbage
    [InlineData("2020-13-01T00:00:00Z", "2099-12-31T23:59:59Z")] // invalid month (13)
    [InlineData("2020-01-32T00:00:00Z", "2099-12-31T23:59:59Z")] // invalid day (32)
    [InlineData("12345", "2099-12-31T23:59:59Z")]                // just numbers
    [InlineData("T00:00:00Z", "2099-12-31T23:59:59Z")]           // time only, no date
    public void EvaluateTimeBasedFlagWithInvalidDateReturnsParseError(string startAt, string endAt)
    {
        var flag = CreateTimeBasedFlagWithDates("invalid-flag", startAt, endAt);
        var flags = new Dictionary<string, Flag> { ["invalid-flag"] = flag };

        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });
        var ctx = new EvaluationContext("user-123");

        var result = evaluator.Evaluate("invalid-flag", Datadog.Trace.FeatureFlags.ValueType.String, "default", ctx);

        result.Value.Should().Be("default");
        Assert.Equal(EvaluationReason.Error, result.Reason);
        result.Error.Should().Be("PARSE_ERROR");
    }

    private static void AssertFlattenContext(Dictionary<string, object?> attrs, Dictionary<string, object?> expected)
    {
        var ctx = new EvaluationContext("structure", attrs);
        var flattened = FeatureFlagsEvaluator.FlattenContext(ctx);

        Assert.Equal(expected.Count, flattened.Count);

        foreach (var pair in expected)
        {
            flattened.TryGetValue(pair.Key, out var actual).Should().BeTrue();
            actual.Should().Be(pair.Value);
        }
    }

    private static Flag CreateTimeBasedFlagWithDates(string key, string startAt, string endAt)
    {
        var variants = new Dictionary<string, Variant>
                       {
                           ["time-limited"] = new Variant("time-limited", "time-limited")
                       };

        var splits = new List<Split> { new() { Shards = [], VariationKey = "time-limited" } };
        var alloc = new Allocation { Key = "time-alloc", StartAt = startAt, EndAt = endAt, Splits = splits, DoLog = false };

        return new Flag { Key = key, Enabled = true, VariationType = Datadog.Trace.FeatureFlags.ValueType.String, Variations = variants, Allocations = [alloc] };
    }
}
#pragma warning restore SA1201 // A method should not follow a class
#pragma warning restore SA1204 // Static elements should appear before instance elements
#pragma warning restore SA1500 // Braces for multi-line statements should not share line
