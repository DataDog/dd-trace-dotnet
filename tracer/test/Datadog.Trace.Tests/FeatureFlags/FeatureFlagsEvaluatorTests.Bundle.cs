// <copyright file="FeatureFlagsEvaluatorTests.Bundle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
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
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1202 // public members should come before private members

    /// <summary>
    /// Known reason overrides where .NET behavior differs from the shared fixture expectations.
    /// .NET returns Error for non-existent flags (the flag is not in config, so evaluation fails),
    /// while the shared fixtures expect DEFAULT (which is the Go behavior).
    /// Key format: "flagName/targetingKey".
    /// </summary>
    private static readonly Dictionary<string, EvaluationReason> KnownReasonOverrides = new()
    {
        { "flag-that-does-not-exist/alice", EvaluationReason.Error },
        { "flag-that-does-not-exist/bob", EvaluationReason.Error },
        { "flag-that-does-not-exist/charlie", EvaluationReason.Error },
    };

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

        // Variant is not present in shared fixtures — only assert when provided
        if (testCase.Result.Variant is not null)
        {
            AssertEqual(testCase.Result.Variant, result.Variant);
        }

        Assert.Equal(testCase.Result.Reason, result.Reason);

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
                // Use JToken.DeepEquals for order-independent JSON comparison
                var actualToken = JToken.Parse(JsonConvert.SerializeObject(obj));
                var expectedToken = expected is JToken jt ? jt : JToken.Parse(expected?.ToString() ?? "null");
                Assert.True(
                    JToken.DeepEquals(expectedToken, actualToken),
                    $"JSON mismatch.\nExpected: {expectedToken}\nActual:   {actualToken}");
            }
            else
            {
                Assert.Equal<object>(expected, obj);
            }
        }
    }

    /// <summary>
    /// Maps SCREAMING_SNAKE reason strings from shared fixtures to PascalCase EvaluationReason enum values.
    /// Shared fixtures (ffe-system-test-data) use SCREAMING_SNAKE: STATIC, SPLIT, TARGETING_MATCH, DEFAULT, ERROR.
    /// .NET EvaluationReason enum uses PascalCase: Static, Split, TargetingMatch, Default, Error.
    /// </summary>
    private static EvaluationReason MapReason(string reason) => reason switch
    {
        "STATIC" => EvaluationReason.Static,
        "SPLIT" => EvaluationReason.Split,
        "TARGETING_MATCH" => EvaluationReason.TargetingMatch,
        "DEFAULT" => EvaluationReason.Default,
        "ERROR" => EvaluationReason.Error,
        "DISABLED" => EvaluationReason.Disabled,
        "CACHED" => EvaluationReason.Cached,
        "UNKNOWN" => EvaluationReason.Unknown,
        // Also accept PascalCase for backwards compatibility
        "Static" => EvaluationReason.Static,
        "Split" => EvaluationReason.Split,
        "TargetingMatch" => EvaluationReason.TargetingMatch,
        "Default" => EvaluationReason.Default,
        "Error" => EvaluationReason.Error,
        "Disabled" => EvaluationReason.Disabled,
        "Cached" => EvaluationReason.Cached,
        "Unknown" => EvaluationReason.Unknown,
        _ => throw new ArgumentException($"Unknown reason: {reason}")
    };

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

    /// <summary>
    /// Resolves the base path for ffe-system-test-data fixtures.
    /// Searches up from the output directory to find the submodule path.
    /// </summary>
    private static string GetFixtureBasePath()
    {
        // When running tests, the output directory is something like:
        // tracer/test/Datadog.Trace.Tests/bin/Debug/net8.0/
        // But the submodule is at:
        // tracer/test/Datadog.Trace.Tests/FeatureFlags/ffe-system-test-data/
        // Try output directory first (CopyToOutputDirectory), then search up.
        var outputDir = AppContext.BaseDirectory;

        // Check if files were copied to output directory
        var outputPath = Path.Combine(outputDir, "FeatureFlags", "ffe-system-test-data");
        if (Directory.Exists(outputPath) && Directory.GetFiles(Path.Combine(outputPath, "evaluation-cases"), "*.json").Length > 0)
        {
            return outputPath;
        }

        // Fall back to searching up from output dir to find the submodule in source tree
        var dir = new DirectoryInfo(outputDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FeatureFlags", "ffe-system-test-data");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "ufc-config.json")))
            {
                return candidate;
            }

            // Also check if we're at the test project root (Datadog.Trace.Tests)
            candidate = Path.Combine(dir.FullName, "tracer", "test", "Datadog.Trace.Tests", "FeatureFlags", "ffe-system-test-data");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "ufc-config.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Cannot find ffe-system-test-data fixture directory. " +
            "Ensure the git submodule is initialized: git submodule update --init");
    }

    private static ServerConfiguration ReadConfig()
    {
        var basePath = GetFixtureBasePath();
        var configPath = Path.Combine(basePath, "ufc-config.json");
        var configContent = File.ReadAllText(configPath);

        var fullObject = JObject.Parse(configContent);

        // Shared fixtures (ffe-system-test-data) use flat format with flags at top level.
        // Old flags-v1.json used nested format: data.attributes.flags.
        var dataToken = fullObject.SelectToken("flags") ?? fullObject.SelectToken("data.attributes.flags");
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
        List<object[]> testData = new List<object[]>();

        var basePath = GetFixtureBasePath();
        var dataDir = Path.Combine(basePath, "evaluation-cases");

        foreach (var filePath in Directory.GetFiles(dataDir, "*.json").OrderBy(f => f))
        {
            var fileName = Path.GetFileName(filePath);
            var content = File.ReadAllText(filePath);
            var rawTestCases = JsonConvert.DeserializeObject<List<RawTestCase>>(content);

            foreach (var raw in rawTestCases!)
            {
                // Determine expected reason: check known overrides first, then map from fixture
                var overrideKey = $"{raw.Flag}/{raw.TargetingKey}";
                EvaluationReason expectedReason;
                if (KnownReasonOverrides.TryGetValue(overrideKey, out var overrideReason))
                {
                    expectedReason = overrideReason;
                }
                else
                {
                    expectedReason = raw.Result?.Reason is not null ? MapReason(raw.Result.Reason) : EvaluationReason.Default;
                }

                var testCase = new TestCase
                {
                    Flag = raw.Flag,
                    VariationType = raw.VariationType,
                    DefaultValue = raw.DefaultValue,
                    TargetingKey = raw.TargetingKey,
                    Attributes = raw.Attributes,
                    Result = new TestCase.Evaluation
                    {
                        Value = raw.Result?.Value,
                        Variant = raw.Result?.Variant,
                        Error = raw.Result?.Error,
                        FlagMetadata = raw.Result?.FlagMetadata,
                        Reason = expectedReason,
                    }
                };

                testData.Add([fileName, testCase]);
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

    /// <summary>
    /// Raw deserialization model where Reason is a string (to handle SCREAMING_SNAKE format from shared fixtures).
    /// </summary>
    internal class RawTestCase
    {
        public string? Flag { get; set; }

        public string? VariationType { get; set; }

        public object? DefaultValue { get; set; }

        public string? TargetingKey { get; set; }

        public Dictionary<string, object?>? Attributes { get; set; }

        public RawEvaluation? Result { get; set; }

        public class RawEvaluation
        {
            public object? Value { get; set; }

            public string? Reason { get; set; }

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
#pragma warning restore SA1202 // public members should come before private members
#pragma warning restore SA1201 // Elements should appear in the correct order
#pragma warning restore SA1204 // Static elements should appear before instance elements
#pragma warning restore SA1500 // Braces for multi-line statements should not share line
