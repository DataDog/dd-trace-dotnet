// <copyright file="FeatureFlagsEvaluatorTests.Bundle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing.Text;
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
    internal static ServerConfiguration _config = ReadConfig();
#pragma warning restore SA1401 // Fields should be private

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
        AssertEqual(testCase.Result.Variant, result.Variant);

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
                var jsonTxt = JToken.Parse(JsonConvert.SerializeObject(obj)).ToString();
                var expectedTxt = expected?.ToString();
                Assert.Equal<object>(expectedTxt, jsonTxt);
            }
            else
            {
                Assert.Equal<object>(expected, obj);
            }
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
        var configContent = ResourceHelper.ReadAllText<FeatureFlagsEvaluatorTests>("resources.config.flags-v1.json");
        var fullObject = JObject.Parse(configContent);
        var dataToken = fullObject.SelectToken("data.attributes.flags");
        var flags = dataToken?.ToObject<Dictionary<string, Flag>>();
        Assert.NotNull(flags);

        foreach (var flag in flags)
        {
            if (flag.Value.Allocations is null) { continue; }
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

        var config = new ServerConfiguration { Flags = flags };

        return config;
    }

    private static List<object[]> GetTestData()
    {
        // This file should regularly be updated from here https://github.com/DataDog/experimental/blob/main/teams/asm/iast/redaction/suite/evidence-redaction-suite.yml

        List<object[]> testData = new List<object[]>();

        foreach (var file in ResourceHelper.EnumFiles<FeatureFlagsEvaluatorTests>("resources.data"))
        {
            var testCases = JsonConvert.DeserializeObject<List<TestCase>>(file.Value);
            foreach (var testCase in testCases!)
            {
                testData.Add([file.Key, testCase]);
            }
        }

        return testData;
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

            public EvaluationReason Reason { get; set; }

            public string? Variant { get; set; }

            public string? Error { get; set; }

            public Dictionary<string, string>? FlagMetadata { get; set; }
        }
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
