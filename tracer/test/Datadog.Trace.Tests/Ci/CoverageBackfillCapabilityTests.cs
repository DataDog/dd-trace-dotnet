// <copyright file="CoverageBackfillCapabilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentVariablesCleaner(
    ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath,
    ConfigurationKeys.CIVisibility.TestSessionCommand,
    "VSTEST_TESTCASEFILTER")]
public class CoverageBackfillCapabilityTests : SettingsTestsBase
{
    [Fact]
    public void ExternalXmlPathMustBeLineVerifiedBeforeSkipping()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/missing-coverage.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("verified line-capable");
    }

    [Fact]
    public void CoverletCollectorIsBackfillableBeforeExternalReportExists()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/missing-coverage.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void MicrosoftCodeCoverageWithoutVerifiedLineXmlIsNotBackfillable()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"Code Coverage\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("Microsoft CodeCoverage");
    }

    [Fact]
    public void TestFilterMakesAggregateCoverageUnsafe()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --filter FullyQualifiedName~Smoke");
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoverage, "1"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("test filter");
    }

    private static TestOptimizationSettings CreateSettings(params (string Key, string Value)[] values)
    {
        var allValues = new (string Key, string Value)[values.Length + 1];
        allValues[0] = (ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
        Array.Copy(values, 0, allValues, 1, values.Length);
        return new TestOptimizationSettings(CreateConfigurationSource(allValues), NullConfigurationTelemetry.Instance);
    }
}
