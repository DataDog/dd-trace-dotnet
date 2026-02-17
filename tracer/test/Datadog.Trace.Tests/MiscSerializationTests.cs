// <copyright file="MiscSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

/// <summary>
/// Baseline serialization tests for miscellaneous JSON usage patterns.
/// Covers: AzureAppServicePerformanceCounters, TracerFlareApi/Manager,
/// OtlpHelpers, ExposureApi settings, FeatureFlagsEvaluator.
/// </summary>
public class MiscSerializationTests
{
    // ===== Pattern: RuntimeMetrics/AzureAppServicePerformanceCounters.cs =====
    // DeserializeObject<PerformanceCountersValue> with [JsonProperty] camelCase names and int properties

    [Fact]
    public void PerformanceCountersValue_Deserialize_CamelCaseProperties()
    {
        // Exact pattern from AzureAppServicePerformanceCounters.cs line 51:
        // var value = JsonConvert.DeserializeObject<PerformanceCountersValue>(rawValue);
        // language=json
        var json = """{"gen0HeapSize":1048576,"gen1HeapSize":2097152,"gen2HeapSize":4194304,"largeObjectHeapSize":8388608}""";

        var result = JsonConvert.DeserializeObject<PerformanceCountersValueTestModel>(json)!;

        result.Gen0Size.Should().Be(1048576);
        result.Gen1Size.Should().Be(2097152);
        result.Gen2Size.Should().Be(4194304);
        result.LohSize.Should().Be(8388608);
    }

    [Fact]
    public void PerformanceCountersValue_Deserialize_ZeroValues()
    {
        // language=json
        var json = """{"gen0HeapSize":0,"gen1HeapSize":0,"gen2HeapSize":0,"largeObjectHeapSize":0}""";
        var result = JsonConvert.DeserializeObject<PerformanceCountersValueTestModel>(json)!;

        result.Gen0Size.Should().Be(0);
        result.Gen1Size.Should().Be(0);
        result.Gen2Size.Should().Be(0);
        result.LohSize.Should().Be(0);
    }

    // ===== Pattern: TracerFlareApi.cs — JObject.Parse(responseContent)["error"]?.ToString() =====

    [Fact]
    public void TracerFlareApi_JObjectParse_ExtractsErrorField()
    {
        // Exact pattern from TracerFlareApi.cs line 79:
        // error = JObject.Parse(responseContent)["error"]?.ToString();
        // language=json
        var responseContent = """{"error":"Invalid case ID"}""";
        var error = JObject.Parse(responseContent)["error"]?.ToString();
        error.Should().Be("Invalid case ID");
    }

    [Fact]
    public void TracerFlareApi_JObjectParse_MissingErrorReturnsNull()
    {
        // language=json
        var responseContent = """{"status":"ok"}""";
        var error = JObject.Parse(responseContent)["error"]?.ToString();
        error.Should().BeNull();
    }

    // ===== Pattern: TracerFlareManager.cs — JObject.Parse + bracket access for nested config =====

    [Fact]
    public void TracerFlareManager_JObjectParse_ExtractsLogLevel()
    {
        // Exact pattern from TracerFlareManager.cs line 364-366:
        // var json = JObject.Parse(EncodingHelpers.Utf8NoBom.GetString(remoteConfig.Contents));
        // var logLevel = json["config"]?["log_level"]?.Value<string>();
        // language=json
        var jsonStr = """{"config":{"log_level":"debug"}}""";
        var json = JObject.Parse(jsonStr);
        var logLevel = json["config"]?["log_level"]?.Value<string>();
        logLevel.Should().Be("debug");
    }

    [Fact]
    public void TracerFlareManager_JObjectParse_MissingConfigReturnsNull()
    {
        var jsonStr = @"{}";
        var json = JObject.Parse(jsonStr);
        var logLevel = json["config"]?["log_level"]?.Value<string>();
        logLevel.Should().BeNull();
    }

    // ===== Pattern: TracerFlareManager.TryDeserialize — JObject.Load(jsonReader) =====

    [Fact]
    public void TracerFlareManager_JObjectLoad_FromStream()
    {
        // Exact pattern from TracerFlareManager.cs line 420-423:
        // using var stream = new MemoryStream(contents);
        // using var streamReader = new StreamReader(stream);
        // using var jsonReader = new JsonTextReader(streamReader);
        // return JObject.Load(jsonReader);
        var contents = System.Text.Encoding.UTF8.GetBytes("""{"config":{"log_level":"trace"}}""");
        using var stream = new MemoryStream(contents);
        using var streamReader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(streamReader);
        var jObject = JObject.Load(jsonReader);

        jObject.Should().NotBeNull();
        jObject["config"]?["log_level"]?.Value<string>().Should().Be("trace");
    }

    // ===== Pattern: Activity/OtlpHelpers.cs — JsonConvert.SerializeObject(value) for IEnumerable =====

    [Fact]
    public void OtlpHelpers_SerializeObject_IEnumerable()
    {
        // Exact pattern from OtlpHelpers.cs line 458:
        // AgentSetOtlpTag(span, key, JsonConvert.SerializeObject(value));
        // Used when value is IEnumerable and allowUnrolling is false
        var value = (object)new List<Dictionary<string, int>> { new() { { "key", 42 } } };
        var json = JsonConvert.SerializeObject(value);
        json.Should().Be("""[{"key":42}]""");
    }

    [Fact]
    public void OtlpHelpers_SerializeObject_SimpleList()
    {
        var value = (object)new[] { "tag1", "tag2", "tag3" };
        var json = JsonConvert.SerializeObject(value);
        json.Should().Be("""["tag1","tag2","tag3"]""");
    }

    // ===== Pattern: CIEnvironmentValues.cs — SerializeObject on string[] and Dictionary =====

    [Fact]
    public void CIEnvironmentValues_SerializeObject_StringArray()
    {
        // Exact pattern from CIEnvironmentValues.cs line 421:
        // JsonConvert.SerializeObject(nodeLabels)
        var nodeLabels = new[] { "linux", "x64", "docker" };
        var json = JsonConvert.SerializeObject(nodeLabels);
        json.Should().Be("""["linux","x64","docker"]""");
    }

    [Fact]
    public void CIEnvironmentValues_SerializeObject_DictionaryStringNullableString()
    {
        // Exact pattern from CIEnvironmentValues.cs line 439:
        // JsonConvert.SerializeObject(variablesToBypass)
        var variablesToBypass = new Dictionary<string, string?>
        {
            { "CI_PIPELINE_ID", "12345" },
            { "CI_JOB_URL", null },
            { "CI_PROJECT_DIR", "/builds/project" },
        };

        var json = JsonConvert.SerializeObject(variablesToBypass);
        json.Should().Contain("\"CI_PIPELINE_ID\":\"12345\"");
        json.Should().Contain("\"CI_JOB_URL\":null");
        json.Should().Contain("\"CI_PROJECT_DIR\":\"/builds/project\"");
    }

    // ===== Pattern: GitlabEnvironmentValues.cs — DeserializeObject<string[]> =====

    [Fact]
    public void GitlabEnvironmentValues_DeserializeObject_StringArray()
    {
        // Exact pattern from GitlabEnvironmentValues.cs line 65:
        // JsonConvert.DeserializeObject<string[]>(runnerTags)
        // language=json
        var runnerTags = """["docker","linux","large"]""";
        var result = JsonConvert.DeserializeObject<string[]>(runnerTags)!;
        result.Should().HaveCount(3);
        result.Should().Contain("docker");
        result.Should().Contain("large");
    }

    // ===== Pattern: ExposureApi.cs — NullValueHandling.Include + SnakeCaseNamingStrategy =====
    // This is DIFFERENT from SerializationHelpers which uses NullValueHandling.Ignore

    [Fact]
    public void ExposureApi_Settings_IncludesNulls_SnakeCase()
    {
        // Exact settings from ExposureApi.cs line 31-38:
        // NullValueHandling = NullValueHandling.Include,
        // ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            }
        };

        var payload = new ExposureRequestTestModel
        {
            Context = new Dictionary<string, string>
            {
                { "service", "my-service" },
                { "env", "prod" },
            },
            Exposures = [new ExposureEventTestModel { Timestamp = 1700000000L, NullField = null }],
        };

        var json = JsonConvert.SerializeObject(payload, settings);

        // Snake case naming
        json.Should().Contain("\"context\":");
        json.Should().Contain("\"exposures\":");
        json.Should().Contain("\"timestamp\":");
        // NullValueHandling.Include — null fields SHOULD be present
        json.Should().Contain("\"null_field\":null");
    }

    [Fact]
    public void ExposureApi_Settings_VsDefaultSettings_NullHandlingDiffers()
    {
        // Demonstrate the difference: ExposureApi includes nulls, DefaultJsonSettings ignores them
        var includeSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
        };

        var ignoreSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
        };

        var payload = new ExposureEventTestModel { Timestamp = 1L, NullField = null };

        var includeJson = JsonConvert.SerializeObject(payload, includeSettings);
        var ignoreJson = JsonConvert.SerializeObject(payload, ignoreSettings);

        includeJson.Should().Contain("\"null_field\":null");
        ignoreJson.Should().NotContain("\"null_field\"");
    }

    // ===== Pattern: FeatureFlagsEvaluator.cs — JObject type check + SerializeObject =====

    [Fact]
    public void FeatureFlagsEvaluator_MapValue_JObject_ToString()
    {
        // Exact pattern from FeatureFlagsEvaluator.cs line 477-479:
        // if (value is JObject) { return value.ToString(); }
        var value = (object)JObject.Parse("""{"key":"val","nested":{"a":1}}""");

        (value is JObject).Should().BeTrue();
        var result = value.ToString();
        result.Should().Contain("\"key\"");
        result.Should().Contain("\"val\"");
    }

    [Fact]
    public void FeatureFlagsEvaluator_MapValue_NonJObject_SerializeObject()
    {
        // Exact pattern from FeatureFlagsEvaluator.cs line 482:
        // var json = JsonConvert.SerializeObject(value);
        var dictValue = (object)new Dictionary<string, object> { { "key", "val" }, { "count", 42 } };

        (dictValue is JObject).Should().BeFalse();
        var json = JsonConvert.SerializeObject(dictValue);
        json.Should().Contain("\"key\":\"val\"");
        json.Should().Contain("\"count\":42");
    }

    // ===== Pattern: AppSec/AppSecRequestContext.cs — SerializeObject on List<object> =====

    [Fact]
    public void AppSecRequestContext_SerializeObject_ListObject()
    {
        // Exact pattern from AppSecRequestContext.cs line 47:
        // var triggers = JsonConvert.SerializeObject(_wafSecurityEvents);
        // span.Tags.SetTag(Tags.AppSecJson, "{\"triggers\":" + triggers + "}");
        var wafSecurityEvents = new List<object>
        {
            new Dictionary<string, object> { { "rule_id", "rule-001" }, { "action", "block" } },
            new Dictionary<string, object> { { "rule_id", "rule-002" }, { "action", "monitor" } },
        };

        var triggers = JsonConvert.SerializeObject(wafSecurityEvents);
        var appSecJson = "{\"triggers\":" + triggers + "}";

        appSecJson.Should().Contain("\"triggers\":");
        appSecJson.Should().Contain("\"rule_id\":\"rule-001\"");
        appSecJson.Should().Contain("\"action\":\"block\"");
    }

    // ===== Pattern: AppSec/Coordinator/SecurityReporter.cs — SerializeObject on object =====

    [Fact]
    public void SecurityReporter_SerializeObject_ArbitraryObject()
    {
        // Exact pattern from SecurityReporter.cs line 249:
        // var serializeObject = JsonConvert.SerializeObject(derivative.Value);
        var derivativeValue = (object)new Dictionary<string, object>
        {
            { "schema", new[] { new Dictionary<string, object> { { "key", new[] { 1, 2, 3 } } } } }
        };

        var json = JsonConvert.SerializeObject(derivativeValue);
        json.Should().Contain("\"schema\"");
        json.Should().Contain("\"key\"");
        // Verify it's valid JSON by parsing
        var parsed = JObject.Parse(json);
        parsed.Should().NotBeNull();
    }

    // ===== Test models mirroring internal types =====

    private sealed class PerformanceCountersValueTestModel
    {
        [JsonProperty("gen0HeapSize")]
        public int Gen0Size { get; set; }

        [JsonProperty("gen1HeapSize")]
        public int Gen1Size { get; set; }

        [JsonProperty("gen2HeapSize")]
        public int Gen2Size { get; set; }

        [JsonProperty("largeObjectHeapSize")]
        public int LohSize { get; set; }
    }

    private sealed class ExposureRequestTestModel
    {
        public Dictionary<string, string> Context { get; set; } = null!;

        public List<ExposureEventTestModel> Exposures { get; set; } = null!;
    }

    private sealed class ExposureEventTestModel
    {
        public long Timestamp { get; set; }

        public string? NullField { get; set; }
    }
}
