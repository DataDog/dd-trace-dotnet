// <copyright file="CoverageBackfillCapabilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
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
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("test filter");
    }

    [Fact]
    public void ItrOnlyCoverageCollectionDoesNotRequireCoverageBackfill()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoverage, "1"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void TargetedProjectMakesAggregateCoverageUnsafe()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test tests/Sample.Tests/Sample.Tests.csproj");
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("targeted project");
    }

    [Fact]
    public void GeneratedCoberturaXmlIsBackfillableWhenNoThresholdIsDetected()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated-cobertura.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output-format cobertura --output /tmp/generated-cobertura.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void ExternalThresholdMakesPostCommandXmlBackfillUnsafe()
    {
        var filePath = Path.GetTempFileName();
        var coverageXml =
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        try
        {
            File.WriteAllText(filePath, coverageXml);

            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage --format cobertura --threshold 80");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("threshold");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static TestOptimizationSettings CreateSettings(params (string Key, string Value)[] values)
    {
        var allValues = new (string Key, string Value)[values.Length + 1];
        allValues[0] = (ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "1");
        Array.Copy(values, 0, allValues, 1, values.Length);
        return new TestOptimizationSettings(CreateConfigurationSource(allValues), NullConfigurationTelemetry.Instance);
    }
}
