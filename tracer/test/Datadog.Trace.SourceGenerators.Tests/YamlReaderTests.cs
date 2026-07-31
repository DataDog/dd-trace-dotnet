// <copyright file="YamlReaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators.Helpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests;

public class YamlReaderTests
{
    private const string YamlWithSensitiveAndOrdinaryEntries = """
                                                               version: '2'
                                                               supportedConfigurations:
                                                                 DD_API_KEY:
                                                                 - implementation: A
                                                                   sensitive: TRUE
                                                                   documentation: API key used to authenticate with Datadog.
                                                                 DD_TRACE_ENABLED:
                                                                 - implementation: A
                                                                   sensitive: false
                                                                   documentation: Enables the tracer.
                                                                 DD_SERVICE:
                                                                 - implementation: A
                                                                   documentation: The service name.
                                                               """;

    [Fact]
    public void ParsesSensitiveMetadata()
    {
        var parsed = YamlReader.ParseSupportedConfigurations(YamlWithSensitiveAndOrdinaryEntries);

        parsed.Configurations["DD_API_KEY"].Sensitive.Should().BeTrue();
        parsed.Configurations["DD_TRACE_ENABLED"].Sensitive.Should().BeFalse();
        parsed.Configurations["DD_SERVICE"].Sensitive.Should().BeFalse();
    }
}
