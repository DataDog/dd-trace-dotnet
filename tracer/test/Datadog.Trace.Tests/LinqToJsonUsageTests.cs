// <copyright file="LinqToJsonUsageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

/// <summary>
/// Baseline tests capturing LINQ-to-JSON usage patterns found across the codebase.
/// These reproduce the exact JObject/JArray/JToken operations that will need
/// replacement during JSON library migration.
/// </summary>
public class LinqToJsonUsageTests
{
    // ===== Pattern: JObject.Parse + bracket access (GithubActionsEnvironmentValues, DiscoveryService) =====

    [Fact]
    public void JObject_Parse_BracketAccess_ExtractsNestedValues()
    {
        // Pattern from GithubActionsEnvironmentValues: JObject.Parse(json)["pull_request"]["head"]["ref"]
        // language=json
        var json = """
            {
                "pull_request": {
                    "head": { "ref": "feature-branch", "sha": "abc123" },
                    "base": { "ref": "main" }
                },
                "ref": "refs/pull/42/merge"
            }
            """;

        var jObject = JObject.Parse(json);
        jObject["pull_request"].Should().NotBeNull();
        jObject["pull_request"]!["head"]!["ref"]!.ToString().Should().Be("feature-branch");
        jObject["pull_request"]!["head"]!["sha"]!.ToString().Should().Be("abc123");
        jObject["pull_request"]!["base"]!["ref"]!.ToString().Should().Be("main");
        jObject["ref"]!.ToString().Should().Be("refs/pull/42/merge");
    }

    [Fact]
    public void JObject_Parse_MissingKey_ReturnsNull()
    {
        var jObject = JObject.Parse("""{"key": "value"}""");
        var result = jObject["nonexistent"];
        result.Should().BeNull();
    }

    // ===== Pattern: JArray cast + Values<string>() (DiscoveryService) =====

    [Fact]
    public void JObject_ArrayProperty_CastToJArray_ExtractsStringValues()
    {
        // Pattern from DiscoveryService: (jObject["endpoints"] as JArray)?.Values<string>().ToArray()
        // language=json
        var json = """
            {
                "endpoints": ["/v0.4/traces", "/v0.6/stats", "/v0.7/config"],
                "client_drop_p0s": true,
                "feature_flags": ["evp_proxy_v4"]
            }
            """;

        var jObject = JObject.Parse(json);
        var endpoints = (jObject["endpoints"] as JArray)?.Values<string>().ToArray();

        endpoints.Should().NotBeNull();
        endpoints.Should().HaveCount(3);
        endpoints.Should().Contain("/v0.4/traces");
        endpoints.Should().Contain("/v0.7/config");

        // Boolean extraction
        jObject["client_drop_p0s"]?.Value<bool>().Should().BeTrue();

        // Missing optional field
        var missing = (jObject["nonexistent"] as JArray)?.Values<string>().ToArray();
        missing.Should().BeNull();
    }

    // ===== Pattern: SelectTokens with JPath (SourceLinkInformationExtractor) =====

    [Fact]
    public void JObject_SelectTokens_WildcardPath_ExtractsValues()
    {
        // Pattern from SourceLinkInformationExtractor: SelectTokens("$.documents.*").FirstOrDefault()?.ToString()
        // language=json
        var json = """
            {
                "documents": {
                    "C:\\dev\\project\\*": "https://raw.githubusercontent.com/org/repo/abc123/*",
                    "D:\\other\\*": "https://raw.githubusercontent.com/org/other/def456/*"
                }
            }
            """;

        var jObject = JObject.Parse(json);
        var firstDocUrl = jObject.SelectTokens("$.documents.*").FirstOrDefault()?.ToString();

        firstDocUrl.Should().NotBeNull();
        firstDocUrl.Should().Contain("raw.githubusercontent.com");

        // Multiple tokens
        var allDocs = jObject.SelectTokens("$.documents.*").Select(t => t.ToString()).ToList();
        allDocs.Should().HaveCount(2);
    }

    [Fact]
    public void JObject_SelectTokens_EmptyDocuments_ReturnsEmpty()
    {
        // language=json
        var json = """{"documents": {}}""";
        var jObject = JObject.Parse(json);
        var result = jObject.SelectTokens("$.documents.*").FirstOrDefault();
        result.Should().BeNull();
    }

    // ===== Pattern: value is JObject type check (FeatureFlagsEvaluator.MapValue) =====

    [Fact]
    public void JObject_TypeCheck_DistinguishesFromPrimitives()
    {
        // Pattern from FeatureFlagsEvaluator: if (value is JObject) value.ToString() else JsonConvert.SerializeObject(value)
        var jsonObject = JObject.Parse("""{"key": "val", "nested": {"a": 1}}""");
        var plainDict = new Dictionary<string, object> { { "key", "val" } };
        var plainString = "hello";
        var plainInt = 42;

        // JObject should be detected
        (jsonObject is JObject).Should().BeTrue();
        jsonObject.ToString().Should().Contain("key");

        // Non-JObject types should NOT match
        ((object)plainDict is JObject).Should().BeFalse();
        ((object)plainString is JObject).Should().BeFalse();
        ((object)plainInt is JObject).Should().BeFalse();

        // When value is JObject, ToString() serializes it
        var jObjAsString = jsonObject.ToString();
        jObjAsString.Should().Contain("\"key\"");
        jObjAsString.Should().Contain("\"val\"");

        // When not JObject, JsonConvert.SerializeObject is used
        var dictJson = JsonConvert.SerializeObject(plainDict);
        dictJson.Should().Contain("\"key\"");
    }

    // ===== Pattern: JToken.FromObject / JToken.Parse (ApmTracingConfigMerger, ConfigurationState) =====

    [Fact]
    public void JToken_Parse_PreservesTypes()
    {
        var stringToken = JToken.Parse("\"hello\"");
        stringToken.Type.Should().Be(JTokenType.String);
        stringToken.Value<string>().Should().Be("hello");

        var intToken = JToken.Parse("42");
        intToken.Type.Should().Be(JTokenType.Integer);
        intToken.Value<int>().Should().Be(42);

        var boolToken = JToken.Parse("true");
        boolToken.Type.Should().Be(JTokenType.Boolean);
        boolToken.Value<bool>().Should().BeTrue();

        var nullToken = JToken.Parse("null");
        nullToken.Type.Should().Be(JTokenType.Null);

        var arrayToken = JToken.Parse("[1,2,3]");
        arrayToken.Type.Should().Be(JTokenType.Array);

        var objectToken = JToken.Parse("""{"k":"v"}""");
        objectToken.Type.Should().Be(JTokenType.Object);
    }

    [Fact]
    public void JToken_FromObject_CreatesCorrectTokenTypes()
    {
        var fromString = JToken.FromObject("hello");
        fromString.Type.Should().Be(JTokenType.String);

        var fromInt = JToken.FromObject(42);
        fromInt.Type.Should().Be(JTokenType.Integer);

        var fromBool = JToken.FromObject(true);
        fromBool.Type.Should().Be(JTokenType.Boolean);

        var fromDict = JToken.FromObject(new Dictionary<string, string> { { "k", "v" } });
        fromDict.Type.Should().Be(JTokenType.Object);

        var fromArray = JToken.FromObject(new[] { 1, 2, 3 });
        fromArray.Type.Should().Be(JTokenType.Array);
    }

    // ===== Pattern: JToken HasValues check (AsmDdProduct) =====

    [Fact]
    public void JToken_HasValues_DistinguishesStringFromObject()
    {
        // Pattern from AsmDdProduct: checking if a JToken is a string vs an object
        var stringToken = JToken.Parse("\"just a string\"");
        var objectToken = JToken.Parse("""{"key":"value"}""");
        var emptyObject = JToken.Parse("{}");

        stringToken.HasValues.Should().BeFalse();
        objectToken.HasValues.Should().BeTrue();
        emptyObject.HasValues.Should().BeFalse();
    }

    // ===== Pattern: DeserializeObject as JToken → SelectToken (JsonConfigurationSource) =====

    [Fact]
    public void DeserializeObject_AsJToken_SupportsSelectToken()
    {
        // Pattern from JsonConfigurationSource constructor:
        // (JToken?)JsonConvert.DeserializeObject(json) → SelectToken(key)
        // language=json
        var json = """{"level1": {"level2": {"value": "deep"}}, "simple": "top"}""";
        var token = (JToken?)JsonConvert.DeserializeObject(json);

        token.Should().NotBeNull();
        token!.SelectToken("simple")?.Value<string>().Should().Be("top");
        token.SelectToken("level1.level2.value")?.Value<string>().Should().Be("deep");
        token.SelectToken("nonexistent").Should().BeNull();
    }

    // ===== Pattern: ToObject<T> (JsonConfigurationSource.ConvertToDictionary) =====

    [Fact]
    public void JToken_ToObject_ConvertsToDictionary()
    {
        // Pattern from JsonConfigurationSource: token.ToObject<ConcurrentDictionary<string, string>>()
        var token = JToken.Parse("""{"key1": "val1", "key2": "val2"}""");
        var dict = token.ToObject<Dictionary<string, string>>()!;

        dict.Should().NotBeNull();
        dict.Should().HaveCount(2);
        dict["key1"].Should().Be("val1");
        dict["key2"].Should().Be("val2");
    }

    // ===== Pattern: ReadAsType<JObject> deserialization =====

    [Fact]
    public void JsonConvert_DeserializeObject_AsJObject_SupportsBracketAccess()
    {
        // Pattern from DiscoveryService and DynamicConfigConfigurationSource
        // language=json
        var json = """{"version": "7.52.0", "endpoints": ["/v0.4/traces"], "config": {"max_eps": 200}}""";
        var jObject = JsonConvert.DeserializeObject<JObject>(json)!;

        jObject["version"]?.Value<string>().Should().Be("7.52.0");
        (jObject["endpoints"] as JArray).Should().ContainSingle();
        jObject["config"]?["max_eps"]?.Value<int>().Should().Be(200);
    }
}
