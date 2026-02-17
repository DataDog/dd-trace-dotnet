// <copyright file="JsonConfigurationSourceSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

/// <summary>
/// Baseline behavioral tests for JsonConfigurationSource's JSON-specific operations.
/// These capture SelectToken (JPath), Value&lt;T&gt;, JTokenType dispatch, and
/// ToObject behavior before any JSON library migration.
/// </summary>
public class JsonConfigurationSourceSerializationTests
{
    private static readonly NullConfigurationTelemetry Telemetry = new();

    // ===== SelectToken / JPath behavior =====

    [Theory]
    [InlineData("simple_key", "simple-value")]
    [InlineData("nested.key", "nested-value")]
    [InlineData("deep.nested.key", "deep-nested-value")]
    public void GetString_DottedPaths_ResolveCorrectly(string key, string expected)
    {
        // language=json
        var json = """
            {
                "simple_key": "simple-value",
                "nested": { "key": "nested-value" },
                "deep": { "nested": { "key": "deep-nested-value" } }
            }
            """;

        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);
        var result = source.GetString(key, Telemetry, validator: null, recordValue: true);

        result.IsValid.Should().BeTrue();
        result.Result.Should().Be(expected);
    }

    [Fact]
    public void GetString_MissingKey_ReturnsNotFound()
    {
        // language=json
        var json = """{"key": "value"}""";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetString("nonexistent", Telemetry, validator: null, recordValue: true);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetString_NullValue_ReturnsNotFound()
    {
        // language=json
        var json = """{"key": null}""";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetString("key", Telemetry, validator: null, recordValue: true);
        result.IsValid.Should().BeFalse();
    }

    // ===== Primitive type extraction =====

    [Theory]
    [InlineData("42", 42)]
    [InlineData("0", 0)]
    [InlineData("-1", -1)]
    public void GetInt32_IntegerValues_ParseCorrectly(string jsonValue, int expected)
    {
        var json = $@"{{""key"": {jsonValue}}}";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetInt32("key", Telemetry, validator: null);
        result.IsValid.Should().BeTrue();
        result.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("1.0", 1.0)]
    [InlineData("-99.99", -99.99)]
    [InlineData("1e-5", 1e-5)]
    public void GetDouble_DoubleValues_ParseCorrectly(string jsonValue, double expected)
    {
        var json = $@"{{""key"": {jsonValue}}}";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetDouble("key", Telemetry, validator: null);
        result.IsValid.Should().BeTrue();
        result.Result.Should().BeApproximately(expected, 1e-10);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void GetBool_BoolValues_ParseCorrectly(string jsonValue, bool expected)
    {
        var json = $@"{{""key"": {jsonValue}}}";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetBool("key", Telemetry, validator: null);
        result.IsValid.Should().BeTrue();
        result.Result.Should().Be(expected);
    }

    // ===== JTokenToString behavior =====

    [Fact]
    public void JTokenToString_StringToken_ReturnsStringValue()
    {
        var token = JToken.Parse("\"hello\"");
        var result = JsonConfigurationSource.JTokenToString(token);
        result.Should().Be("hello");
    }

    [Fact]
    public void JTokenToString_IntegerToken_ReturnsJsonString()
    {
        var token = JToken.Parse("42");
        var result = JsonConfigurationSource.JTokenToString(token);
        result.Should().Be("42");
    }

    [Fact]
    public void JTokenToString_ObjectToken_ReturnsMinifiedJson()
    {
        var token = JToken.Parse("""{ "key": "value" }""");
        var result = JsonConfigurationSource.JTokenToString(token);
        result.Should().Be("""{"key":"value"}""");
    }

    [Fact]
    public void JTokenToString_ArrayToken_ReturnsMinifiedJson()
    {
        var token = JToken.Parse("[1, 2, 3]");
        var result = JsonConfigurationSource.JTokenToString(token);
        result.Should().Be("[1,2,3]");
    }

    [Fact]
    public void JTokenToString_BoolToken_ReturnsJsonString()
    {
        var token = JToken.Parse("true");
        var result = JsonConfigurationSource.JTokenToString(token);
        result.Should().Be("true");
    }

    [Fact]
    public void JTokenToString_NullToken_ReturnsNull()
    {
        var token = JToken.Parse("null");
        var result = JsonConfigurationSource.JTokenToString(token);
        result.Should().BeNull();
    }

    [Fact]
    public void JTokenToString_NullInput_ReturnsNull()
    {
        var result = JsonConfigurationSource.JTokenToString(null);
        result.Should().BeNull();
    }

    // ===== GetDictionary with JSON objects =====

    [Fact]
    public void GetDictionary_JsonObject_ParsesAsStringDictionary()
    {
        // language=json
        var json = """{"tags": {"env": "prod", "version": "1.0"}}""";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetDictionary("tags", Telemetry, validator: null);
        result.IsValid.Should().BeTrue();
        result.Result.Should().ContainKey("env").WhoseValue.Should().Be("prod");
        result.Result.Should().ContainKey("version").WhoseValue.Should().Be("1.0");
    }

    [Fact]
    public void GetDictionary_StringValue_ParsesAsKeyValuePairs()
    {
        // language=json
        var json = """{"tags": "k1:v1,k2:v2"}""";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetDictionary("tags", Telemetry, validator: null);
        result.IsValid.Should().BeTrue();
        result.Result.Should().ContainKey("k1").WhoseValue.Should().Be("v1");
        result.Result.Should().ContainKey("k2").WhoseValue.Should().Be("v2");
    }

    [Fact]
    public void GetDictionary_MissingKey_ReturnsNotFound()
    {
        var json = @"{}";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetDictionary("nonexistent", Telemetry, validator: null);
        result.IsValid.Should().BeFalse();
    }

    // ===== GetAs with complex types =====

    [Fact]
    public void GetAs_ArrayAsString_ReturnsSerializedJson()
    {
        // language=json
        var json = """{"rules": [{"sample_rate": 0.5, "service": "*"}]}""";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetAs<string>(
            "rules",
            Telemetry,
            converter: s => ParsingResult<string>.Success(s),
            validator: null,
            recordValue: true);

        result.IsValid.Should().BeTrue();
        // JTokenToString serializes arrays/objects back to JSON
        result.Result.Should().Contain("sample_rate");
        result.Result.Should().Contain("0.5");
    }

    [Fact]
    public void GetAs_NestedPath_ReturnsValue()
    {
        // language=json
        var json = """{"apm_configuration": {"dd_trace_sample_rate": "0.8"}}""";
        var source = new JsonConfigurationSource(json, ConfigurationOrigins.Default);

        var result = source.GetAs<double>(
            "apm_configuration.dd_trace_sample_rate",
            Telemetry,
            converter: s => double.TryParse(s, out var d) ? ParsingResult<double>.Success(d) : ParsingResult<double>.Failure(),
            validator: null,
            recordValue: true);

        result.IsValid.Should().BeTrue();
        result.Result.Should().Be(0.8);
    }
}
