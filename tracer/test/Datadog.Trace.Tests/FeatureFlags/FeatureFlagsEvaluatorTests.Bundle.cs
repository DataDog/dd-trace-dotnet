// <copyright file="FeatureFlagsEvaluatorTests.Bundle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary> FeatureFlagsEvaluator bundled tests </summary>
public partial class FeatureFlagsEvaluatorTests
{
#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1500 // Braces for multi-line statements should not share line
#pragma warning disable SA1401 // Fields should be private
    public static List<object[]> TestData = GetTestData();
    public static List<object[]> RegexConformanceTestData = GetRegexConformanceTestData();
    internal static ServerConfiguration _config = ReadConfig();
#pragma warning restore SA1401 // Fields should be private

    public enum CanonicalEvaluationReason
    {
        DEFAULT,

        STATIC,

        TARGETING_MATCH,

        SPLIT,

        DISABLED,

        CACHED,

        UNKNOWN,

        ERROR
    }

    [SkippableTheory]
    [MemberData(nameof(TestData))]
    public void BundledTest(string description, TestCase? testCase)
    {
        Assert.NotNull(testCase);
        Assert.NotNull(testCase.Flag);
        Assert.NotNull(testCase.Result);

        var evaluator = new FeatureFlagsEvaluator(null, _config);
        var ctx = new EvaluationContext(testCase.TargetingKey ?? string.Empty, testCase.Attributes);

        var type = GetVariationType(testCase.VariationType);

        var result = evaluator.Evaluate(testCase.Flag, type, testCase.DefaultValue, ctx);
        Assert.NotNull(result);

        if (testCase.Result.Value is null || !testCase.Result.Value.Equals(result.Value))
        {
            _ = 0;
        }

        AssertEqual(testCase.Result.Value, result.Value);
        if (testCase.Result.Variant is not null)
        {
            AssertEqual(testCase.Result.Variant, result.Variant);
        }

        Assert.Equal(testCase.Result.Reason, ToCanonicalReason(result.Reason));

        Assert.NotNull(description);

        void AssertEqual(object? expected, object? obj)
        {
            if (expected is null)
            {
                Assert.Equal<object>(expected, obj);
            }
            else if (type == Trace.FeatureFlags.ValueType.Integer && obj is int intObj)
            {
                Assert.Equal(Convert.ToInt32(expected), intObj);
            }
            else if (type == Trace.FeatureFlags.ValueType.Json)
            {
                // Normalize BCL structure and Expected Json
                var actualJson = JToken.Parse(JsonConvert.SerializeObject(obj));
                var expectedJson = JToken.Parse(expected?.ToString() ?? "null");
                Assert.True(JToken.DeepEquals(expectedJson, actualJson), $"Expected {expectedJson}, got {actualJson}");
            }
            else
            {
                Assert.Equal<object>(expected, obj);
            }
        }
    }

    [Fact]
    public void RegexConformanceFixtureShape()
    {
        var cases = ReadRegexConformanceFixture().Cases!;

        Assert.NotEmpty(cases);
    }

    [Theory]
    [MemberData(nameof(RegexConformanceTestData))]
    public void RegexConformance(RegexConformanceCase testCase)
    {
        Assert.NotNull(testCase.Id);
        Assert.NotNull(testCase.RawPattern);
        Assert.NotNull(testCase.Input);
        Assert.NotNull(testCase.ExpectedCompile);

        // UFC supplies the raw pattern. ConditionConfiguration is the production normalization boundary
        // before System.Text.RegularExpressions.Regex compiles and evaluates it.
        var evaluator = new FeatureFlagsEvaluator(null, CreateRegexConfiguration(testCase.RawPattern!));
        var context = new EvaluationContext(
            testCase.Id!,
            new Dictionary<string, object?> { ["input"] = testCase.Input });

        var result = evaluator.Evaluate("regex-conformance", Trace.FeatureFlags.ValueType.Boolean, false, context);

        if (testCase.ExpectedCompile == false)
        {
            Assert.Equal(EvaluationReason.Error, result.Reason);
            Assert.Equal("PARSE_ERROR", result.Error);
            return;
        }

        Assert.Equal(EvaluationReason.TargetingMatch, result.Reason);
        if (testCase.ExpectedMatch.HasValue)
        {
            Assert.Equal(testCase.ExpectedMatch.Value, result.Value);
        }
    }

    private static Trace.FeatureFlags.ValueType GetVariationType(string? variationType)
    {
        return variationType switch
        {
            "INTEGER" => Trace.FeatureFlags.ValueType.Integer,
            "NUMERIC" => Trace.FeatureFlags.ValueType.Numeric,
            "STRING" => Trace.FeatureFlags.ValueType.String,
            "BOOLEAN" => Trace.FeatureFlags.ValueType.Boolean,
            "JSON" => Trace.FeatureFlags.ValueType.Json,
            _ => throw new NotImplementedException(),
        };
    }

    private static ServerConfiguration ReadConfig()
    {
        // Read config
        var configContent = ResourceHelper.ReadAllText<FeatureFlagsEvaluatorTests>("ffe_system_test_data.ufc-config.json");
        var fullObject = JObject.Parse(configContent);
        var config = fullObject.ToObject<ServerConfiguration>();
        Assert.NotNull(config);
        Assert.NotNull(config.Flags);

        foreach (var flag in config.Flags)
        {
            if (flag.Value?.Allocations is null) { continue; }
            foreach (var allocation in flag.Value.Allocations)
            {
                allocation.StartAt = FixDateString(allocation.StartAt);
                allocation.EndAt = FixDateString(allocation.EndAt);

                if (allocation.Rules is null) { continue; }
                foreach (var rule in allocation.Rules)
                {
                    if (rule.Conditions is null) { continue; }
                    foreach (var condition in rule.Conditions)
                    {
                        if (condition.Value is not JArray jArray) { continue; }
                        var arr = jArray.ToObject<object[]>();
                        condition.Value = arr;
                    }
                }
            }
        }

        static string? FixDateString(string? dateString)
        {
            if (string.IsNullOrEmpty(dateString)) { return null; }

            if (DateTime.TryParseExact(
                dateString!,
                "MM/dd/yyyy HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dt))
            {
                return dt.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
            }

            return dateString;
        }

        return config;
    }

    private static List<object[]> GetTestData()
    {
        List<object[]> testData = new List<object[]>();

        foreach (var file in ResourceHelper.EnumFiles<FeatureFlagsEvaluatorTests>("ffe_system_test_data.evaluation_cases").OrderBy(static file => file.Key))
        {
            var testCases = JsonConvert.DeserializeObject<List<TestCase>>(file.Value);
            foreach (var testCase in testCases!)
            {
                testData.Add([file.Key, testCase]);
            }
        }

        Assert.NotEmpty(testData);
        return testData;
    }

    private static List<object[]> GetRegexConformanceTestData()
    {
        var testData = new List<object[]>();
        foreach (var testCase in ReadRegexConformanceFixture().Cases!)
        {
            if (testCase.ExpectedCompile.HasValue)
            {
                testData.Add([testCase]);
            }
        }

        return testData;
    }

    private static RegexConformanceFixture ReadRegexConformanceFixture()
    {
        var fixtureContent = ResourceHelper.ReadAllText<FeatureFlagsEvaluatorTests>(
            "ffe_system_test_data.regex_conformance.targeting-regex-conformance.json");
        var fixture = JsonConvert.DeserializeObject<RegexConformanceFixture>(fixtureContent);
        Assert.NotNull(fixture);
        Assert.NotNull(fixture.Cases);
        return fixture;
    }

    private static ServerConfiguration CreateRegexConfiguration(string pattern)
    {
        var flag = new Flag
        {
            Key = "regex-conformance",
            Enabled = true,
            VariationType = Trace.FeatureFlags.ValueType.Boolean,
            Variations = new Dictionary<string, Variant>
            {
                ["matched"] = new Variant { Key = "matched", Value = true },
                ["not-matched"] = new Variant { Key = "not-matched", Value = false },
            },
            Allocations =
            [
                CreateRegexAllocation("matched", ConditionOperator.MATCHES, pattern),
                CreateRegexAllocation("not-matched", ConditionOperator.NOT_MATCHES, pattern),
            ],
        };

        return new ServerConfiguration
        {
            Flags = new Dictionary<string, Flag> { ["regex-conformance"] = flag },
        };
    }

    private static Allocation CreateRegexAllocation(string key, ConditionOperator conditionOperator, string pattern)
    {
        return new Allocation
        {
            Key = key,
            Rules = [new Rule([new ConditionConfiguration { Attribute = "input", Operator = conditionOperator, Value = pattern }])],
            Splits = [new Split { VariationKey = key, Shards = [] }],
            DoLog = false,
        };
    }

    private static CanonicalEvaluationReason ToCanonicalReason(EvaluationReason reason)
    {
        return reason switch
        {
            EvaluationReason.Default => CanonicalEvaluationReason.DEFAULT,
            EvaluationReason.Static => CanonicalEvaluationReason.STATIC,
            EvaluationReason.TargetingMatch => CanonicalEvaluationReason.TARGETING_MATCH,
            EvaluationReason.Split => CanonicalEvaluationReason.SPLIT,
            EvaluationReason.Disabled => CanonicalEvaluationReason.DISABLED,
            EvaluationReason.Cached => CanonicalEvaluationReason.CACHED,
            EvaluationReason.Unknown => CanonicalEvaluationReason.UNKNOWN,
            EvaluationReason.Error => CanonicalEvaluationReason.ERROR,
            _ => throw new NotImplementedException(),
        };
    }

    public class TestCase
    {
        public string? Flag { get; set; }

        public string? VariationType { get; set; }

        public object? DefaultValue { get; set; }

        public string? TargetingKey { get; set; }

        public Dictionary<string, object?>? Attributes { get; set; }

        public Evaluation? Result { get; set; }

        public class Evaluation
        {
            public object? Value { get; set; }

            public CanonicalEvaluationReason Reason { get; set; }

            public string? Variant { get; set; }

            public string? Error { get; set; }

            public Dictionary<string, string>? FlagMetadata { get; set; }
        }
    }

    public class RegexConformanceFixture
    {
        public List<RegexConformanceCase>? Cases { get; set; }
    }

    public class RegexConformanceCase
    {
        public string? Id { get; set; }

        public string? RawPattern { get; set; }

        public string? Input { get; set; }

        public bool? ExpectedCompile { get; set; }

        public bool? ExpectedMatch { get; set; }
    }

    /*
         "flag": "boolean-one-of-matches",
        "variationType": "INTEGER",
        "defaultValue": 0,
        "targetingKey": "alice",
        "attributes": {
          "one_of_flag": true
        },
        "result": {
          "value": 1,
          "variant": "1",
          "flagMetadata": {
            "allocationKey": "1-for-one-of",
            "variationType": "number",
            "doLog": true
          }
        }

     */
}
#pragma warning restore SA1204 // Static elements should appear before instance elements
#pragma warning restore SA1500 // Braces for multi-line statements should not share line
