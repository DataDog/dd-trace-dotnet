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
    ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand,
    ConfigurationKeys.VstestTestCaseFilter)]
public class CoverageBackfillCapabilityTests : SettingsTestsBase
{
    [Fact]
    public void ExistingExternalXmlPathMustBeLineVerifiedBeforeSkipping()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """<results><modules><module lines_covered="1" lines_partially_covered="0" lines_not_covered="1" /></modules></results>""");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("aggregate-only");
        }
        finally
        {
            File.Delete(filePath);
        }
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
    public void InternalCoverageBackfillCommandTakesPrecedenceOverPublicSessionCommand()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dd-trace ci run -- dotnet test --filter FullyQualifiedName~Smoke");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
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
    public void TargetedProjectDoesNotDisableScopedCoverageBackfill()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test tests/Sample.Tests/Sample.Tests.csproj");
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void TargetedSolutionDoesNotDisableScopedCoverageBackfill()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test tests/Sample.sln --collect \"XPlat Code Coverage\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void VstestAssemblyPathDoesNotDisableCoverageBackfill()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "vstest /tmp/Sample.Tests.dll --collect:\"XPlat Code Coverage\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedXmlReportCanBeBackfilledAfterCommandWhenNoThresholdIsConfigured()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated-cobertura.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output-format cobertura --output /tmp/generated-cobertura.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void UnknownGeneratedXmlReportIsUnsafeUntilLineCapabilityCanBeVerified()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated-coverage.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage --output /tmp/generated-coverage.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("must exist before skipping");
    }

    [Fact]
    public void GeneratedExternalReportMustBeXml()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated-coverage.json");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage --output /tmp/generated-coverage.json");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("XML report");
    }

    [Fact]
    public void MicrosoftCodeCoverageCanBeBackfilledThroughVanguardXml()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"Code Coverage\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CoverageIpcWaitsForSelectedToolEvenWhenItrSkippingIsDisabled()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        var settings = CreateSettingsWithSkipping(testsSkippingEnabled: false);

        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
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
        return CreateSettingsWithSkipping(testsSkippingEnabled: true, values);
    }

    private static TestOptimizationSettings CreateSettingsWithSkipping(bool testsSkippingEnabled, params (string Key, string Value)[] values)
    {
        var allValues = new (string Key, string Value)[values.Length + 1];
        allValues[0] = (ConfigurationKeys.CIVisibility.TestsSkippingEnabled, testsSkippingEnabled ? "1" : "0");
        Array.Copy(values, 0, allValues, 1, values.Length);
        return new TestOptimizationSettings(CreateConfigurationSource(allValues), NullConfigurationTelemetry.Instance);
    }
}
