// <copyright file="UfcConfigurationParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class UfcConfigurationParserTests
{
    private const string ValidEnvelope = """
        { "data": { "type": "universal-flag-configuration",
                    "attributes": { "format": "SERVER", "createdAt": "2025-01-01T00:00:00Z",
                                    "environment": { "name": "production" }, "flags": {} } } }
        """;

    private const string Attributes = """
        { "format": "SERVER", "createdAt": "2025-01-01T00:00:00Z",
          "environment": { "name": "production" }, "flags": {} }
        """;

    public static IEnumerable<object?[]> ObserveFullEvaluationDataCases()
    {
        yield return [null, false];
        yield return ["false", false];
        yield return ["true", true];
        yield return ["null", false];
        yield return ["\"true\"", false];
        yield return ["1", false];
        yield return ["{}", false];
        yield return ["[]", false];
    }

    [Fact]
    public void ParsesValidEnvelope()
    {
        UfcConfigurationParser.TryParse(ValidEnvelope, out var configuration, out var error)
            .Should().BeTrue();

        error.Should().BeNull();
        configuration.Should().NotBeNull();
        configuration!.Environment!.Name.Should().Be("production");
        configuration.Flags.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"data\": ")]
    public void RejectsMalformedJson(string? body)
    {
        UfcConfigurationParser.TryParse(body, out var configuration, out var error).Should().BeFalse();

        configuration.Should().BeNull();
        error.Should().Be("Malformed UFC payload");
    }

    [Theory]
    // A raw UFC document is rejected too, so every source agrees on one wire format.
    [InlineData(Attributes)]
    // Wrong resource type
    [InlineData("""{ "data": { "type": "wrong-type", "attributes": { "format": "SERVER", "createdAt": "x", "environment": { "name": "prod" }, "flags": {} } } }""")]
    // Missing data
    [InlineData("""{ "meta": {} }""")]
    // data is not an object
    [InlineData("""{ "data": "string" }""")]
    // data.type is not a string (object)
    [InlineData("""{ "data": { "type": { "nested": true }, "attributes": { "format": "SERVER", "createdAt": "x", "environment": { "name": "prod" }, "flags": {} } } }""")]
    // data.type is not a string (array)
    [InlineData("""{ "data": { "type": [1, 2], "attributes": { "format": "SERVER", "createdAt": "x", "environment": { "name": "prod" }, "flags": {} } } }""")]
    public void RejectsInvalidEnvelope(string body)
    {
        UfcConfigurationParser.TryParse(body, out var configuration, out var error).Should().BeFalse();

        configuration.Should().BeNull();
        error.Should().Be("Expected a JSON:API Universal Flag Configuration resource");
    }

    [Theory]
    // Missing format
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "createdAt": "x", "environment": { "name": "prod" }, "flags": {} } } }""")]
    // Missing createdAt
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "environment": { "name": "prod" }, "flags": {} } } }""")]
    // Missing environment
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "x", "flags": {} } } }""")]
    // environment.name is not a string
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "x", "environment": { "name": 123 }, "flags": {} } } }""")]
    // Missing flags
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "x", "environment": { "name": "prod" } } } }""")]
    // flags is not an object
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "x", "environment": { "name": "prod" }, "flags": [] } } }""")]
    public void RejectsInvalidAttributes(string body)
    {
        UfcConfigurationParser.TryParse(body, out var configuration, out var error).Should().BeFalse();

        configuration.Should().BeNull();
        error.Should().Be("Expected a Universal Flag Configuration v1 object");
    }

    [Fact]
    public void ParsesFlagsFromEnvelope()
    {
        var body = """
            { "data": { "type": "universal-flag-configuration",
                        "attributes": { "format": "SERVER", "createdAt": "2025-01-01T00:00:00Z",
                                        "environment": { "name": "production" },
                                        "flags": { "test-flag": { "key": "test-flag", "enabled": true, "variationType": "BOOLEAN" } } } } }
            """;

        UfcConfigurationParser.TryParse(body, out var configuration, out _).Should().BeTrue();

        configuration!.Flags.Should().NotBeNull();
        configuration!.Flags!.Should().ContainKey("test-flag");
        configuration!.Flags!["test-flag"].Enabled.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ObserveFullEvaluationDataCases))]
    public void AgentlessParserOnlyBooleanTrueEnablesFullEvaluationData(string? jsonValue, bool expected)
    {
        var privacyProperty = jsonValue is null ? string.Empty : $", \"observeFullEvaluationData\": {jsonValue}";
        var body = $$"""
            { "data": { "type": "universal-flag-configuration",
                        "attributes": { "format": "SERVER", "createdAt": "2025-01-01T00:00:00Z",
                                        "environment": { "name": "production" },
                                        "flags": { "test-flag": { "key": "test-flag", "enabled": true, "variationType": "BOOLEAN" } }
                                        {{privacyProperty}} } } }
            """;

        UfcConfigurationParser.TryParse(body, out var configuration, out var error).Should().BeTrue();

        error.Should().BeNull();
        configuration!.ObserveFullEvaluationData.Should().Be(expected);
        configuration.Flags!["test-flag"].ObserveFullEvaluationData.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ObserveFullEvaluationDataCases))]
    public void RemoteConfigurationDeserializerOnlyBooleanTrueEnablesFullEvaluationData(string? jsonValue, bool expected)
    {
        var privacyProperty = jsonValue is null ? string.Empty : $", \"observeFullEvaluationData\": {jsonValue}";
        var body = $$"""
            { "format": "SERVER", "createdAt": "2025-01-01T00:00:00Z",
              "environment": { "name": "production" },
              "flags": { "test-flag": { "key": "test-flag", "enabled": true, "variationType": "BOOLEAN" } }
              {{privacyProperty}} }
            """;

        var configuration = JsonConvert.DeserializeObject<ServerConfiguration>(body);

        configuration.Should().NotBeNull();
        configuration!.ObserveFullEvaluationData.Should().Be(expected);
        configuration.Flags!["test-flag"].ObserveFullEvaluationData.Should().Be(expected);
    }

    [Fact]
    public void MergePreservesConsentFromEachFlagsSourceConfiguration()
    {
        var full = JsonConvert.DeserializeObject<ServerConfiguration>(
            """{ "observeFullEvaluationData": true, "flags": { "full": { "key": "full", "enabled": true } } }""")!;
        var protectedConfig = JsonConvert.DeserializeObject<ServerConfiguration>(
            """{ "observeFullEvaluationData": false, "flags": { "protected": { "key": "protected", "enabled": true } } }""")!;
        var merged = new ServerConfiguration();

        merged.Merge(full);
        merged.Merge(protectedConfig);

        merged.Flags!["full"].ObserveFullEvaluationData.Should().BeTrue();
        merged.Flags["protected"].ObserveFullEvaluationData.Should().BeFalse();
    }

    [Fact]
    public void MergeRootConsent_IsOrderIndependentAndFailsClosedAcrossSources()
    {
        var full = JsonConvert.DeserializeObject<ServerConfiguration>(
            """{ "observeFullEvaluationData": true, "flags": { "full": { "key": "full", "enabled": true } } }""")!;
        var protectedConfig = JsonConvert.DeserializeObject<ServerConfiguration>(
            """{ "observeFullEvaluationData": false, "flags": { "protected": { "key": "protected", "enabled": true } } }""")!;
        var fullThenProtected = new ServerConfiguration();
        var protectedThenFull = new ServerConfiguration();

        fullThenProtected.Merge(full);
        fullThenProtected.Merge(protectedConfig);
        protectedThenFull.Merge(protectedConfig);
        protectedThenFull.Merge(full);

        fullThenProtected.ObserveFullEvaluationData.Should().BeFalse();
        protectedThenFull.ObserveFullEvaluationData.Should().BeFalse();
    }

    [Fact]
    public void MergeRootConsent_RemainsFullOnlyWhenEverySourceOptsIn()
    {
        var first = JsonConvert.DeserializeObject<ServerConfiguration>(
            """{ "observeFullEvaluationData": true, "flags": { "one": { "key": "one", "enabled": true } } }""")!;
        var second = JsonConvert.DeserializeObject<ServerConfiguration>(
            """{ "observeFullEvaluationData": true, "flags": { "two": { "key": "two", "enabled": true } } }""")!;
        var merged = new ServerConfiguration();

        merged.Merge(first);
        merged.Merge(second);

        merged.ObserveFullEvaluationData.Should().BeTrue();
    }
}
