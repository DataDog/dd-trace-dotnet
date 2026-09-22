// <copyright file="CoverageReporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CoverageReporterTests : SettingsTestsBase
{
    [Fact]
    public void DefaultHandlerUsesLightweightHandlerWhenSkippingIsEnabledWithoutGlobalCoveragePath()
    {
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1"));

        CoverageReporter.CreateDefaultHandler(settings).Should().BeOfType<DefaultCoverageEventHandler>();
    }

    [Fact]
    public void DefaultHandlerUsesGlobalHandlerWhenSkippingIsEnabledWithGlobalCoveragePath()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1"),
            (ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageReporter.CreateDefaultHandler(settings).Should().BeOfType<DefaultWithGlobalCoverageEventHandler>();
    }

    [Fact]
    public void DefaultHandlerKeepsGlobalHandlerWhenSkippingIsDisabled()
    {
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "0"));

        CoverageReporter.CreateDefaultHandler(settings).Should().BeOfType<DefaultWithGlobalCoverageEventHandler>();
    }

    [Fact]
    public void DefaultHandlerKeepsGlobalHandlerWhenSkippingSettingIsMissing()
    {
        var settings = CreateSettings();

        CoverageReporter.CreateDefaultHandler(settings).Should().BeOfType<DefaultWithGlobalCoverageEventHandler>();
    }

    private static TestOptimizationSettings CreateSettings(params (string Key, string Value)[] values)
    {
        return new TestOptimizationSettings(CreateConfigurationSource(values), NullConfigurationTelemetry.Instance);
    }
}
