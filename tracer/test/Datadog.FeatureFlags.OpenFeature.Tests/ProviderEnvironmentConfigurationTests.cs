// <copyright file="ProviderEnvironmentConfigurationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias Tracer;

using System;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class ProviderEnvironmentConfigurationTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("T", true)]
    [InlineData("t", true)]
    [InlineData("Y", true)]
    [InlineData("y", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("False", false)]
    [InlineData("no", false)]
    [InlineData("NO", false)]
    [InlineData("F", false)]
    [InlineData("f", false)]
    [InlineData("N", false)]
    [InlineData("n", false)]
    [InlineData("0", false)]
    [InlineData("", null)]
    [InlineData("invalid", null)]
    [InlineData("2", null)]
    [InlineData(" true ", null)]
    [InlineData(" false ", null)]
    public void SharedParserPreservesTracerBooleanSemantics(string value, bool? expected)
    {
        Assert.Equal(expected, BooleanConfigurationParser.Parse(value));
        Assert.Equal(expected, Tracer::Datadog.Trace.ExtensionMethods.StringExtensions.ToBoolean(value));
    }

    [Fact]
    public void SharedParserAndTracerExtensionRejectNull()
    {
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => BooleanConfigurationParser.Parse(null)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => Tracer::Datadog.Trace.ExtensionMethods.StringExtensions.ToBoolean(null)).ParamName);
    }

    [Fact]
    public void ProviderKeyMatchesGeneratedRegistryConstant()
    {
        Assert.Equal(
            Tracer::Datadog.Trace.Configuration.ConfigurationKeys.FeatureFlags.EvaluationTelemetryPerSubjectOptOutEnabled,
            ProviderEnvironmentConfiguration.EvaluationTelemetryOptOutEnabled);
    }
}
