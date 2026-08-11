// <copyright file="UfcConfigurationParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.FeatureFlags.Agentless;
using FluentAssertions;
using Xunit;

using FeatureFlagsValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class UfcConfigurationParserTests
{
    private const string Attributes = """
        {
          "format": "SERVER",
          "createdAt": "2025-01-01T00:00:00Z",
          "environment": { "name": "production" },
          "flags": {
            "test-flag": {
              "key": "test-flag",
              "enabled": true,
              "variationType": "BOOLEAN",
              "variations": { "on": { "key": "on", "value": true } },
              "allocations": []
            }
          }
        }
        """;

    private const string Envelope = $$"""
        { "data": { "type": "universal-flag-configuration", "id": "1", "attributes": {{Attributes}} } }
        """;

    [Fact]
    public void ParsesJsonApiEnvelope()
    {
        UfcConfigurationParser.TryParse(Envelope, out var configuration, out var error).Should().BeTrue();

        error.Should().BeNull();
        configuration!.Format.Should().Be("SERVER");
        configuration.CreatedAt.Should().Be("2025-01-01T00:00:00Z");
        configuration.Environment!.Name.Should().Be("production");
        configuration.Flags.Should().ContainKey("test-flag");
        configuration.Flags!["test-flag"].VariationType.Should().Be(FeatureFlagsValueType.Boolean);
    }

    [Fact]
    public void AcceptsAnEmptyFlagSet()
    {
        var body = """
            { "data": { "type": "universal-flag-configuration",
                        "attributes": { "format": "SERVER", "createdAt": "2025-01-01T00:00:00Z",
                                        "environment": { "name": "production" }, "flags": {} } } }
            """;

        UfcConfigurationParser.TryParse(body, out var configuration, out _).Should().BeTrue();

        configuration!.Flags.Should().BeEmpty();
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
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    [InlineData("{}")]
    [InlineData("{ \"data\": [] }")]
    [InlineData("{ \"data\": { \"type\": \"something-else\", \"attributes\": {} } }")]
    public void RejectsANonJsonApiPayload(string body)
    {
        UfcConfigurationParser.TryParse(body, out var configuration, out var error).Should().BeFalse();

        configuration.Should().BeNull();
        error.Should().Be("Expected a JSON:API Universal Flag Configuration resource");
    }

    [Theory]
    // Missing or wrongly typed members of the v1 contract.
    [InlineData("""{ "data": { "type": "universal-flag-configuration" } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": [] } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "createdAt": "t", "environment": { "name": "p" }, "flags": {} } } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "environment": { "name": "p" }, "flags": {} } } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "t", "flags": {} } } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "t", "environment": {}, "flags": {} } } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "t", "environment": { "name": 1 }, "flags": {} } } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "t", "environment": { "name": "p" } } } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": "SERVER", "createdAt": "t", "environment": { "name": "p" }, "flags": [] } } }""")]
    [InlineData("""{ "data": { "type": "universal-flag-configuration", "attributes": { "format": 1, "createdAt": "t", "environment": { "name": "p" }, "flags": {} } } }""")]
    public void RejectsAPayloadThatIsNotUfcV1(string body)
    {
        UfcConfigurationParser.TryParse(body, out var configuration, out var error).Should().BeFalse();

        configuration.Should().BeNull();
        error.Should().Be("Expected a Universal Flag Configuration v1 object");
    }
}
