// <copyright file="CoverageBackfillCapabilityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
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
    ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand,
    PlatformKeys.VstestTestCaseFilter)]
public class CoverageBackfillCapabilityTests : SettingsTestsBase
{
    [Fact]
    public void ExistingExternalXmlPathMustBeLineVerifiedBeforeSkipping()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.xml");
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
    public void ExistingExternalXmlWithUnsafeSourcePathDoesNotAllowSkipping()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.xml");
        try
        {
            var coverageXml =
                """
                <coverage line-rate="0" lines-valid="1" lines-covered="0">
                  <packages>
                    <package name="sample">
                      <classes>
                        <class name="Calculator" filename="../src/Calculator.cs">
                          <lines>
                            <line number="1" hits="0" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(filePath, coverageXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("source paths");
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

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\"")]
    [InlineData("dotnet test /p:CollectCoverage=true")]
    public void CoverletCoverageDoesNotTrustExistingExternalXmlReportItDoesNotWrite(string commandLine)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.xml");
        try
        {
            var coverageXml =
                """
                <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
                  <packages>
                    <package name="sample">
                      <classes>
                        <class name="Calculator" filename="src/Calculator.cs">
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
            File.WriteAllText(filePath, coverageXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("current command");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\"", false)]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\"", true)]
    [InlineData("dotnet test /p:CollectCoverage=true", false)]
    [InlineData("dotnet test /p:CollectCoverage=true", true)]
    public void CoverletCoverageRequiresExternalCoveragePathToBeXml(string commandLine, bool writeExistingReport)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.json");
        try
        {
            if (writeExistingReport)
            {
                File.WriteAllText(filePath, "{}");
            }

            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("XML report");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoverletMsBuildGeneratedExternalReportMustBeXml()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.json");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput={filePath}");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("XML report");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void NonXmlExternalCoveragePathIsNotProcessed()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json");

        CoverageBackfillCapability.CanProcessExternalCoveragePathForCurrentCommand("/tmp/generated-coverage.json", out var reason).Should().BeFalse();
        reason.Should().Contain("XML report");
    }

    [Fact]
    public void DotnetVstestConsoleDllCoverletCollectorRequiresCoverageBackfill()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet /tmp/sdk/vstest.console.dll Sample.Tests.dll /Collect:\"XPlat Code Coverage\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
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
    public void CommandLineIsCachedBetweenCapabilityChecks()
    {
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();

            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();

            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
        }
        finally
        {
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
        }
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

    [Theory]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-class Sample.Tests")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-class=Sample.Tests")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-method Smoke")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-method:Smoke")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-namespace Sample.Tests")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-trait Category=Smoke")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-not-class Sample.Tests")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-not-method Slow")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-not-namespace Sample.SlowTests")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-not-trait Category=Slow")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -- --filter-query /[category=smoke]")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura --filter-uid test-uid")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura --treenode-filter /root/child")]
    public void TestingPlatformFilterOptionsMakeGeneratedXmlCoverageUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("test filter");
    }

    [Theory]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -p:TestingPlatformCommandLineArguments=\"--filter-trait Category=Smoke\"")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -p:TestingPlatformCommandLineArguments=--filter-trait=Category=Integration")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -p:TestingPlatformCommandLineArguments=--filter%20FullyQualifiedName~Smoke")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura --property:TestingPlatformCommandLineArguments=\"--filter-query /[category=smoke]\"")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura /p:TestingPlatformCommandLineArguments=--filter-uid=test-uid")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -p:TestingPlatformCommandLineArguments=--filter-trait\"")]
    public void TestingPlatformCommandLineArgumentsFilterMakesGeneratedXmlCoverageUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("test filter");
    }

    [Fact]
    public void TestingPlatformCommandLineArgumentsWithoutFilterDoesNotMakeGeneratedXmlCoverageUnsafe()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -p:TestingPlatformCommandLineArguments=\"--results-directory /tmp/--filter-results --report-trx\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/tmp/--filter-results/Sample.Tests.dll")]
    [InlineData("/tmp/--filter-class-results/Sample.Tests.dll")]
    [InlineData("/tmp/--filter-uid-results/Sample.Tests.dll")]
    [InlineData("/tmp/--treenode-filter-results/Sample.Tests.dll")]
    [InlineData("/tmp/-class-results/Sample.Tests.dll")]
    [InlineData("/tmp/-trait-results/Sample.Tests.dll")]
    public void FilterOptionTextInsidePathDoesNotMakeAggregateCoverageUnsafe(string testAssemblyPath)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test {testAssemblyPath}");
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet test -f net8.0")]
    [InlineData("dotnet test -f:net8.0")]
    [InlineData("dotnet test -f=net8.0")]
    [InlineData("dotnet test -fnet8.0")]
    public void ShortFrameworkOptionMakesAggregateCoverageUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -framework net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -framework:net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -framework=net8.0")]
    public void SingleDashFrameworkOptionMakesAggregateCoverageUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -fl")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -flp:logfile=msbuild.log")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -flp=logfile=msbuild.log")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -fileLogger")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -fileLoggerParameters:logfile=msbuild.log")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -fileLoggerParameters=logfile=msbuild.log")]
    public void MsBuildFileLoggerOptionsDoNotMakeCoverageBackfillUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet-coverage collect -f coverage --output /tmp/generated.coverage")]
    [InlineData("dotnet-coverage collect -f xml --output /tmp/generated.xml")]
    [InlineData("dotnet-coverage collect -fcobertura --output /tmp/generated.xml")]
    public void DotnetCoverageKnownShortFormatDoesNotGetParsedAsTargetFramework(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("external XML report path");
        reason.Should().NotContain("target-framework");
    }

    [Fact]
    public void DotnetCoverageNestedDotnetTestShortFrameworkOptionMakesAggregateCoverageUnsafe()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect dotnet test -f net8.0 --output /tmp/generated.xml");
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Theory]
    [InlineData("--collect\nXPlat Code Coverage\n--filter\nFullyQualifiedName~Smoke", "test filter")]
    [InlineData("--collect\nXPlat Code Coverage\n--framework\nnet8.0", "target-framework")]
    [InlineData("--collect\nXPlat Code Coverage\n/p:TargetFramework=net8.0", "target-framework")]
    public void ResponseFileSelectionMakesAggregateCoverageUnsafe(string responseFileArguments, string expectedReason)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            File.WriteAllText(Path.Combine(workingDirectory, "coverage.rsp"), responseFileArguments);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test @coverage.rsp");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain(expectedReason);
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ResponseFileCommentedSelectionDoesNotMakeAggregateCoverageUnsafe()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var responseFileContents =
                """
                # --filter FullyQualifiedName~Smoke
                --collect
                XPlat Code Coverage
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "coverage.rsp"), responseFileContents);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test @coverage.rsp");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ResponseFilePreservesSpacesWithinCollectorArgument()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            File.WriteAllText(Path.Combine(workingDirectory, "coverage.rsp"), "--collect:XPlat Code Coverage");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test @coverage.rsp");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void NestedResponseFileResolvesFromCommandWorkingDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var subDirectory = Path.Combine(workingDirectory, "sub");
            Directory.CreateDirectory(subDirectory);
            File.WriteAllText(Path.Combine(subDirectory, "outer.rsp"), "@inner.rsp");
            File.WriteAllText(Path.Combine(workingDirectory, "inner.rsp"), "--collect:XPlat Code Coverage");
            File.WriteAllText(Path.Combine(subDirectory, "inner.rsp"), "--filter\nFullyQualifiedName~Smoke");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test @sub/outer.rsp");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void UnexpandedResponseFileMakesAggregateCoverageUnsafe()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test @missing.rsp");
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("response file");
    }

    [Fact]
    public void QuotedAtLiteralInResponseFileDoesNotExpandOrFailClosed()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            File.WriteAllText(Path.Combine(workingDirectory, "Smoke"), "--filter FullyQualifiedName~Smoke");
            var responseFileContents =
                """
                test
                --collect
                "XPlat Code Coverage"
                --logger
                "@Smoke"
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "runner.rsp"), responseFileContents);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet @runner.rsp");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -p:TestingPlatformCommandLineArguments=@missing.rsp")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -p:TestingPlatformCommandLineArguments=@missing.rsp\"")]
    public void TestingPlatformCommandLineArgumentsUnexpandedResponseFileFailsClosed(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("response file");
    }

    [Theory]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -p:TestingPlatformCommandLineArguments=@mtp.rsp")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -p:TestingPlatformCommandLineArguments=@mtp.rsp\"")]
    public void TestingPlatformCommandLineArgumentsExpandedResponseFileFilterFailsClosed(string commandLine)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            File.WriteAllText(Path.Combine(workingDirectory, "mtp.rsp"), "--filter-trait\nCategory=Smoke");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("test filter");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void TestingPlatformCommandLineArgumentsExpandedResponseFileWithoutFilterDoesNotFailClosed()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var responseFileContents =
                """
                # --filter-trait Category=Smoke
                --results-directory
                /tmp/--filter-results
                --report-trx
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "mtp.rsp"), responseFileContents);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura -p:TestingPlatformCommandLineArguments=@mtp.rsp");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void TestingPlatformCommandLineArgumentsCoverageResponseFileCanBackfillGeneratedXml()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var responseFileContents =
                """
                --coverage
                --coverage-output
                /tmp/generated.xml
                --coverage-output-format
                cobertura
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "mtp.rsp"), responseFileContents);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test -p:TestingPlatformCommandLineArguments=@mtp.rsp");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
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

    [Theory]
    [InlineData("dotnet test \"/tmp/XPlat Code Coverage/Sample.Tests.dll\"")]
    [InlineData("dotnet test \"/tmp/Code Coverage/Sample.Tests.dll\"")]
    [InlineData("dotnet test \"/tmp/coverlet.collector/Sample.Tests.dll\"")]
    [InlineData("dotnet test \"/tmp/coverlet.msbuild/Sample.Tests.csproj\"")]
    [InlineData("dotnet test --filter \"DisplayName~XPlat Code Coverage\"")]
    [InlineData("dotnet test --filter \"DisplayName~coverlet.collector\"")]
    [InlineData("dotnet test --logger \"trx;LogFileName=Code Coverage.trx\"")]
    [InlineData("dotnet test --collect \"My Code Coverage Tool\"")]
    [InlineData("dotnet test --collect \"Code CoverageX\"")]
    [InlineData("dotnet test --collect \"XPlat Code CoverageX\"")]
    [InlineData("dotnet test --collect \"DatadogCoverage\"")]
    [InlineData("dotnet test /p:PackageReference=coverlet.msbuild")]
    [InlineData("dotnet test /p:SomeProperty=CollectCoverage=true")]
    [InlineData("dotnet test /p:CollectCoverage=false")]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CollectCoverage=false")]
    [InlineData("dotnet test /p:CollectCoverage=true;CollectCoverage=false")]
    [InlineData("dotnet test --property CollectCoverage=true --property CollectCoverage=false")]
    [InlineData("dotnet test --property SomeProperty=CollectCoverage=true")]
    [InlineData("dotnet test --property CollectCoverage=false")]
    [InlineData("dotnet test --property-name CollectCoverage=true")]
    [InlineData("dotnet test --propertyish:CollectCoverage=true")]
    [InlineData("external-coverage --property CollectCoverage=true")]
    [InlineData("external-coverage --collect \"XPlat Code Coverage\"")]
    [InlineData("dotnet --list-runtimes test --collect \"XPlat Code Coverage\"")]
    [InlineData("dotnet vstest sample.dll /p:CollectCoverage=true")]
    [InlineData("dotnet vstest sample.dll --property CollectCoverage=true")]
    public void CoverageToolTextInsideUnrelatedArgumentsDoesNotSelectCoverageReportSource(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\"")]
    [InlineData("dotnet -d test --collect \"XPlat Code Coverage\"")]
    [InlineData("dotnet --diagnostics test --collect \"XPlat Code Coverage\"")]
    [InlineData("dotnet --info test --collect \"XPlat Code Coverage\"")]
    [InlineData("dotnet --version test --collect \"XPlat Code Coverage\"")]
    [InlineData("dotnet test --collect:\"XPlat Code Coverage\"")]
    [InlineData("dotnet test --collect:\"XPlat Code Coverage;IncludeTestAssembly=true\"")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage;IncludeTestAssembly=true\"")]
    [InlineData("dotnet test --collect coverlet.collector")]
    [InlineData("dotnet test -p:VSTestCollect=\"XPlat Code Coverage\"")]
    [InlineData("dotnet test -p:VSTestCollect=\"coverlet.collector\"")]
    [InlineData("vstest Sample.dll /Collect:\"XPlat Code Coverage\"")]
    [InlineData("VSTest.Console.Arm64.exe Sample.dll /Collect:\"XPlat Code Coverage\"")]
    [InlineData("dotnet vstest Sample.dll --collect:\"XPlat Code Coverage\"")]
    [InlineData("msbuild Sample.csproj -t:VSTest -p:VSTestCollect=\"XPlat Code Coverage\"")]
    [InlineData("dotnet msbuild Sample.csproj -target:VSTest -p:VSTestCollect=\"coverlet.collector\"")]
    [InlineData("msbuild Sample.csproj -t:Restore;VSTest -p:VSTestCollect=\"XPlat Code Coverage\"")]
    public void CollectOptionSelectsCoverageReportSource(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("XPlat Code Coverage")]
    [InlineData("Code Coverage;Format=xml")]
    public void VstestCollectDirectoryBuildPropsPropertySelectsCoverageReportSource(string vstestCollect)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                $$"""
                  <Project>
                    <PropertyGroup>
                      <VSTestCollect>{{vstestCollect}}</VSTestCollect>
                    </PropertyGroup>
                  </Project>
                  """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void VstestCollectDirectoryBuildPropsPropertyDoesNotOverrideCommandLineEmpty()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                """
                <Project>
                  <PropertyGroup>
                    <VSTestCollect>XPlat Code Coverage</VSTestCollect>
                  </PropertyGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:VSTestCollect=");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("<VSTestTestCaseFilter>FullyQualifiedName~Smoke</VSTestTestCaseFilter>", "test filter")]
    [InlineData("<VSTestCLIRunSettings>RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke</VSTestCLIRunSettings>", "runsettings testcase filter")]
    public void VstestFilterDirectoryBuildPropsPropertyMakesCoverageBackfillUnsafe(string filterPropertyXml, string expectedReason)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                $$"""
                  <Project>
                    <PropertyGroup>
                      <VSTestCollect>XPlat Code Coverage</VSTestCollect>
                      {{filterPropertyXml}}
                    </PropertyGroup>
                  </Project>
                  """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain(expectedReason);
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void VstestFilterProjectPropertyMakesCoverageBackfillUnsafeWhenCoverageIsSelectedByDirectoryBuildProps()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                """
                <Project>
                  <PropertyGroup>
                    <VSTestCollect>XPlat Code Coverage</VSTestCollect>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <VSTestTestCaseFilter>FullyQualifiedName~Smoke</VSTestTestCaseFilter>
                  </PropertyGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("test filter");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("VSTestTestCaseFilter=FullyQualifiedName~Smoke", "/p:VSTestTestCaseFilter=")]
    [InlineData("VSTestCLIRunSettings=RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke", "/p:VSTestCLIRunSettings=")]
    public void VstestFilterDirectoryBuildPropsPropertyDoesNotOverrideCommandLineEmpty(string filterProperty, string commandLineOverride)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var separatorIndex = filterProperty.IndexOf('=');
            var propertyName = filterProperty.Substring(0, separatorIndex);
            var propertyValue = filterProperty.Substring(separatorIndex + 1);
            var propsXml =
                $$"""
                  <Project>
                    <PropertyGroup>
                      <VSTestCollect>XPlat Code Coverage</VSTestCollect>
                      <{{propertyName}}>{{propertyValue}}</{{propertyName}}>
                    </PropertyGroup>
                  </Project>
                  """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test Sample.Tests.csproj {commandLineOverride}");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("dotnet test --collect \"Code Coverage\"")]
    [InlineData("dotnet test -p:VSTestCollect=\"Code Coverage\"")]
    [InlineData("vstest Sample.dll /Collect:\"Code Coverage\"")]
    [InlineData("vstest Sample.dll /EnableCodeCoverage")]
    [InlineData("VSTest.Console.Arm64 Sample.dll /Collect:\"Code Coverage\"")]
    [InlineData("VSTest.Console.Arm64.exe Sample.dll /EnableCodeCoverage")]
    [InlineData("msbuild Sample.csproj -t:VSTest -p:VSTestCollect=\"Code Coverage\"")]
    public void MicrosoftCodeCoverageCollectRequiresXmlRunSettings(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("XML output");
    }

    [Fact]
    public void DotnetCoverageSettingsDoesNotSelectVstestCodeCoverage()
    {
        var runSettingsPath = WriteTempRunSettings("Code Coverage", dataCollectorFormat: "xml");
        var outputPath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.xml");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet-coverage collect -s \"{runSettingsPath}\" -o \"{outputPath}\" -f cobertura dotnet test");
            var settings = CreateSettings();

            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void DotnetCoverageSettingsDoesNotSelectVstestRunSettings()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage", testCaseFilter: "FullyQualifiedName~Smoke");
        var outputPath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.xml");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet-coverage collect --settings \"{runSettingsPath}\" --output \"{outputPath}\" --output-format cobertura dotnet test");
            var settings = CreateSettings();

            CoverageBackfillCapability.ShouldWaitForCoverletXmlFallback(settings).Should().BeFalse();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("supported external XML report path");
            reason.Should().NotContain("runsettings testcase filter");
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void DotnetCoverageNestedDotnetTestSettingsSelectsVstestCodeCoverage()
    {
        var runSettingsPath = WriteTempRunSettings("Code Coverage", dataCollectorFormat: "xml");
        var outputPath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.xml");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet-coverage collect -o \"{outputPath}\" -f cobertura dotnet test --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Theory]
    [InlineData("dotnet test /p:CollectCoverage=true")]
    [InlineData("dotnet -d test /p:CollectCoverage=true")]
    [InlineData("dotnet --diagnostics test /p:CollectCoverage=true")]
    [InlineData("dotnet --info test /p:CollectCoverage=true")]
    [InlineData("dotnet --version test /p:CollectCoverage=true")]
    [InlineData("dotnet test -p:CollectCoverage=true")]
    [InlineData("dotnet test /p=CollectCoverage=true")]
    [InlineData("dotnet test -p=CollectCoverage=true")]
    [InlineData("dotnet test /p CollectCoverage=true")]
    [InlineData("dotnet test -p CollectCoverage=true")]
    [InlineData("dotnet test /property:CollectCoverage=true")]
    [InlineData("dotnet test -property:CollectCoverage=true")]
    [InlineData("dotnet test --property:CollectCoverage=true")]
    [InlineData("dotnet test /property=CollectCoverage=true")]
    [InlineData("dotnet test -property=CollectCoverage=true")]
    [InlineData("dotnet test --property=CollectCoverage=true")]
    [InlineData("dotnet test /property CollectCoverage=true")]
    [InlineData("dotnet test -property CollectCoverage=true")]
    [InlineData("dotnet test --property CollectCoverage=true")]
    [InlineData("dotnet test /p:CollectCoverage=false /p:CollectCoverage=true")]
    [InlineData("dotnet test /p:CollectCoverage=false,CollectCoverage=true")]
    [InlineData("dotnet test \"/p:CollectCoverage=true\"")]
    [InlineData("dotnet test --property \"CollectCoverage=false,CollectCoverage=true\"")]
    [InlineData("dotnet test /p --property CollectCoverage=true")]
    [InlineData("dotnet test /p /p:CollectCoverage=true")]
    public void MsBuildCollectCoveragePropertySelectsCoverageReportSource(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CoverletMsBuildProjectPropertySelectsCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildProjectTargetAfterBlameHangDumpTypeSelectsCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --blame-hang-dump-type full Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildDirectoryBuildPropsPropertySelectsCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            var directoryBuildProps =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), directoryBuildProps);
            File.WriteAllText(Path.Combine(projectDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test tests/Sample.Tests/Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildDirectoryBuildPropsPathSelectsCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var customBuildDirectory = Path.Combine(workingDirectory, "build");
        try
        {
            Directory.CreateDirectory(customBuildDirectory);
            var propsXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            var propsPath = Path.Combine(customBuildDirectory, "Coverage.props");
            File.WriteAllText(propsPath, propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test Sample.Tests.csproj /p:DirectoryBuildPropsPath={propsPath}");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildImportDirectoryBuildPropsFalseDisablesDirectoryBuildProps()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:ImportDirectoryBuildProps=false");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildProjectPropertyDoesNotOverrideCommandLineFalse()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var directoryBuildProps =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), directoryBuildProps);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:CollectCoverage=false");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildProjectPropertyRequiresCoverletMsBuildReference()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildConditionalFalseProjectPropertyDoesNotSelectCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage Condition="'false' == 'true'">true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildUnknownConditionProjectPropertySelectsCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage Condition="Exists('coverage.flag')">true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildUnknownConditionPackageReferenceSelectsCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" Condition="Exists('coverage.flag')" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildUnknownConditionFalsePropertyDoesNotOverrideActiveCoverage()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                    <CollectCoverage Condition="Exists('missing.flag')">false</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildProjectConditionUsesDotnetTestConfigurationOption()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj -c Debug");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildProjectConditionUsesDefaultConfigurationProperty()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildDirectoryBuildPropsConditionDoesNotUseDefaultConfigurationProperty()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                """
                <Project>
                  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildDirectoryBuildTargetsConditionUsesDefaultConfigurationProperty()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var targetsXml =
                """
                <Project>
                  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.targets"), targetsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildDirectoryBuildTargetsPathSelectsCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var customBuildDirectory = Path.Combine(workingDirectory, "build");
        try
        {
            Directory.CreateDirectory(customBuildDirectory);
            var targetsXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            var targetsPath = Path.Combine(customBuildDirectory, "Coverage.targets");
            File.WriteAllText(targetsPath, targetsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test Sample.Tests.csproj /p:DirectoryBuildTargetsPath={targetsPath}");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildImportDirectoryBuildTargetsFalseDisablesDirectoryBuildTargets()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var targetsXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.targets"), targetsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:ImportDirectoryBuildTargets=false");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("dotnet test Sample.Tests.csproj -c Debug /p:Configuration=Release", true)]
    [InlineData("dotnet test Sample.Tests.csproj /p:Configuration=Release -c Debug", true)]
    public void CoverletMsBuildProjectConditionLetsExplicitConfigurationPropertyOverrideDotnetTestOption(string commandLine, bool expectedBackfillRequired)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().Be(expectedBackfillRequired);
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().Be(expectedBackfillRequired);
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildProjectConditionUsesDotnetTestFrameworkOptionBeforeUnsafeCheck()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj -f net8.0");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("target-framework");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildMultiTargetProjectConditionedCollectCoverageSelectsCoverageReportSourceWithoutFrameworkOption()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
                  </PropertyGroup>
                  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("dotnet test Sample.Tests.csproj -f net9.0")]
    [InlineData("dotnet test Sample.Tests.csproj --framework net9.0")]
    public void CoverletMsBuildMultiTargetProjectConditionedCollectCoverageDoesNotSelectUnselectedDotnetTestFramework(string commandLine)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
                  </PropertyGroup>
                  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildMultiTargetProjectConditionedNonLineThresholdBlocksBackfillWithoutFrameworkOption()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
                  </PropertyGroup>
                  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
                    <CollectCoverage>true</CollectCoverage>
                    <Threshold>80</Threshold>
                    <ThresholdType>branch</ThresholdType>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:CoverletOutputFormat=cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("threshold");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildMultiTargetProjectConditionedOutputPathCanOwnExternalCoveragePath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
                  </PropertyGroup>
                  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
                    <CollectCoverage>true</CollectCoverage>
                    <CoverletOutput>results/</CoverletOutput>
                    <CoverletOutputFormat>cobertura</CoverletOutputFormat>
                    <Threshold>80</Threshold>
                    <ThresholdType>line</ThresholdType>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            var externalCoveragePath = Path.Combine(workingDirectory, "results", "coverage.cobertura.xml");
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, externalCoveragePath);
            var settings = CreateSettings();

            CoverageBackfillCapability.CanProcessExternalCoveragePathForCurrentCommand(externalCoveragePath, out var reason).Should().BeTrue(reason);
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out reason).Should().BeTrue(reason);
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildTargetFrameworkConditionWithoutMultiTargetDoesNotSelectCoverageReportSource()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildProjectConditionSupportsCompoundAndExpression()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup Condition="'$(Configuration)' == 'Release' And '$(TargetFramework)' == 'net8.0'">
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:Configuration=Release /p:TargetFramework=net8.0");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("target-framework");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("'$(Configuration)' == 'Release' And '$(Platform)' == 'AnyCPU'", true)]
    [InlineData("'$(Configuration)' == 'Debug' Or '$(Platform)' == 'AnyCPU'", true)]
    [InlineData("('$(Configuration)' == 'Release' And '$(Platform)' == 'x64') Or '$(Platform)' == 'AnyCPU'", true)]
    [InlineData("'$(Configuration)' == 'Debug' And '$(Platform)' == 'AnyCPU'", false)]
    [InlineData("'$(Configuration)' == 'Debug' Or '$(Platform)' == 'x64'", false)]
    public void CoverletMsBuildProjectCompoundConditionControlsCoverageReportSource(string condition, bool expectedBackfillRequired)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                $$"""
                  <Project>
                    <PropertyGroup Condition="{{condition}}">
                      <CollectCoverage>true</CollectCoverage>
                    </PropertyGroup>
                    <ItemGroup>
                      <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                    </ItemGroup>
                  </Project>
                  """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:Configuration=Release /p:Platform=AnyCPU");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().Be(expectedBackfillRequired);
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().Be(expectedBackfillRequired);
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("dotnet test --coverage")]
    [InlineData("dotnet -d test --coverage")]
    [InlineData("dotnet --diagnostics test --coverage")]
    [InlineData("dotnet --info test --coverage")]
    [InlineData("dotnet --version test --coverage")]
    [InlineData("dotnet test --coverlet")]
    [InlineData("dotnet test -- --coverage")]
    [InlineData("dotnet test -- --coverlet")]
    [InlineData("dotnet /tmp/Sample.Tests.dll --coverage")]
    [InlineData("dotnet ./Sample.Tests.dll --coverage")]
    [InlineData("/tmp/Sample.Tests --coverage")]
    [InlineData("./Sample.Tests --coverage")]
    [InlineData("dotnet exec /tmp/Sample.Tests.dll --coverage")]
    [InlineData("dotnet exec /tmp/Sample.Tests.dll --coverlet")]
    [InlineData("dotnet exec --runtimeconfig /tmp/Sample.Tests.runtimeconfig.json --depsfile /tmp/Sample.Tests.deps.json /tmp/Sample.Tests.dll --coverlet")]
    [InlineData("dotnet run --project /tmp/Sample.Tests/Sample.Tests.csproj --coverage")]
    [InlineData("dotnet run -p /tmp/Sample.Tests/Sample.Tests.csproj --coverage")]
    [InlineData("dotnet run --project /tmp/Sample.Tests/Sample.Tests.csproj --coverlet")]
    [InlineData("dotnet run --project /tmp/Sample.Tests/Sample.Tests.csproj -- --coverlet")]
    [InlineData("dotnet test -p:TestingPlatformCommandLineArguments=--coverage")]
    [InlineData("dotnet test -p:TestingPlatformCommandLineArguments=--coverlet")]
    public void TestingPlatformCoverageSelectsReportSourceButRequiresExternalXmlPath(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsTestingPlatformCoverageCommand(commandLine).Should().BeTrue();
        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("external XML report path");
    }

    [Fact]
    public void TestingPlatformCommandLineArgumentsDirectoryBuildPropsCanBackfillGeneratedXml()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var coveragePath = Path.Combine(workingDirectory, "generated.xml");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                $$"""
                  <Project>
                    <PropertyGroup>
                      <TestingPlatformCommandLineArguments>--coverage --coverage-output "{{coveragePath}}" --coverage-output-format cobertura</TestingPlatformCommandLineArguments>
                    </PropertyGroup>
                  </Project>
                  """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, coveragePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void TestingPlatformCommandLineArgumentsDirectoryBuildPropsDoesNotOverrideCommandLineEmpty()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                """
                <Project>
                  <PropertyGroup>
                    <TestingPlatformCommandLineArguments>--coverage</TestingPlatformCommandLineArguments>
                  </PropertyGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:TestingPlatformCommandLineArguments=");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("dotnet test --coverage-output-format cobertura")]
    [InlineData("dotnet test --coverage-output /tmp/generated.xml")]
    [InlineData("dotnet test -- --coverage-output-format cobertura")]
    [InlineData("dotnet test -- --coverage-output /tmp/generated.xml")]
    [InlineData("dotnet test --coverlet-output-format cobertura")]
    [InlineData("dotnet test --coverlet-file-prefix generated")]
    [InlineData("dotnet test -- --coverlet-output-format cobertura")]
    [InlineData("dotnet test -- --coverlet-file-prefix generated")]
    [InlineData("dotnet exec /tmp/Sample.Tests.dll --coverlet-output-format cobertura")]
    [InlineData("dotnet exec /tmp/Sample.Tests.dll --coverlet-file-prefix generated")]
    [InlineData("dotnet exec --runtimeconfig /tmp/Sample.Tests.runtimeconfig.json /tmp/Sample.Tests.dll --coverlet-output-format cobertura")]
    [InlineData("dotnet run --project /tmp/Sample.Tests/Sample.Tests.csproj --coverlet-output-format cobertura")]
    [InlineData("dotnet run --project /tmp/Sample.Tests/Sample.Tests.csproj -- --coverlet-output-format cobertura")]
    [InlineData("dotnet test -p:TestingPlatformCommandLineArguments=\"--coverage-output /tmp/generated.xml --coverage-output-format cobertura\"")]
    [InlineData("dotnet test -p:TestingPlatformCommandLineArguments=--coverlet-output-format=cobertura")]
    public void TestingPlatformCoverageOutputOptionsWithoutCoverageFlagDoNotSelectReportSource(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsTestingPlatformCoverageCommand(commandLine).Should().BeFalse();
        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet run --project /tmp/--coverlet/Sample.Tests.csproj")]
    [InlineData("dotnet run -p /tmp/--coverlet/Sample.Tests.csproj")]
    [InlineData("dotnet run --environment COVERLET_ARGS=--coverlet")]
    [InlineData("dotnet run -f net8.0 -c Release --no-build")]
    [InlineData("dotnet run --force --project /tmp/Sample.Tests/Sample.Tests.csproj")]
    [InlineData("dotnet run --no-dependencies --project /tmp/Sample.Tests/Sample.Tests.csproj")]
    [InlineData("dotnet run --force --no-dependencies -f net8.0 -c Release --no-build")]
    public void DotnetRunOwnOptionsDoNotSelectTestingPlatformCoverage(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet run --project /tmp/Sample.Tests/Sample.Tests.csproj --force -- --coverlet")]
    [InlineData("dotnet run -p /tmp/Sample.Tests/Sample.Tests.csproj --force -- --coverlet")]
    [InlineData("dotnet run --project /tmp/Sample.Tests/Sample.Tests.csproj --no-dependencies -- --coverage")]
    [InlineData("dotnet run --force --no-dependencies --project /tmp/Sample.Tests/Sample.Tests.csproj -- --coverage")]
    public void DotnetRunKnownOwnFlagsAllowTestingPlatformCoverageAfterAppArgumentDelimiter(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsTestingPlatformCoverageCommand(commandLine).Should().BeTrue();
        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("external XML report path");
    }

    [Fact]
    public void DotnetCoverageCollectDoesNotTreatDotnetRunForceAsCoverageRelevantChildCommand()
    {
        var command = CoverageBackfillCommandLine.Parse("dotnet-coverage collect \"dotnet run --force\"");

        command.GetDotnetCoverageCollectChildCommands().Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet test /p:CollectCoverage%3dtrue")]
    [InlineData("dotnet test --property:CollectCoverage%3dtrue")]
    public void EscapedMsBuildPropertySeparatorDoesNotSelectCoverageReportSource(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
    }

    [Theory]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CollectCoverage")]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CollectCoverage=")]
    [InlineData("dotnet test /p:CollectCoverage=true,CollectCoverage")]
    [InlineData("dotnet test /p:CollectCoverage=true;CollectCoverage=")]
    [InlineData("dotnet test --property CollectCoverage=true --property CollectCoverage")]
    [InlineData("dotnet test --property CollectCoverage=true --property CollectCoverage=")]
    public void MsBuildCollectCoveragePropertyUsesLastValueEvenWhenEmptyOrMissing(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
    }

    [Theory]
    [InlineData("dotnet test -p:VSTestCollect=\"XPlat Code Coverage\" -p:VSTestCollect=")]
    [InlineData("dotnet test -p:VSTestCollect=\"Code Coverage\" -p:VSTestCollect=")]
    [InlineData("dotnet test -p:VSTestCollect=\"XPlat Code Coverage\" -p:VSTestCollect")]
    public void MsBuildVSTestCollectPropertyUsesLastValueEvenWhenEmptyOrMissing(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" /p:TargetFramework=net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" /p:TargetFrameworks=net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" /p:TargetFrameworkVersion=v4.8")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -property:TargetFramework=net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -property:TargetFrameworks=net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -property:TargetFrameworkVersion=v4.8")]
    [InlineData("dotnet test -p:VSTestCollect=\"XPlat Code Coverage\" -p:TargetFramework=net8.0")]
    public void MsBuildTargetFrameworkPropertyMakesCoverageBackfillUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Fact]
    public void DotnetCoverageNestedMsBuildTargetFrameworksPropertyMakesExternalCoverageBackfillUnsafe()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test /p:TargetFrameworks=net8.0\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" /p:VSTestTestCaseFilter=FullyQualifiedName~Smoke")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -property:VSTestTestCaseFilter=FullyQualifiedName~Smoke")]
    [InlineData("dotnet test -p:VSTestCollect=\"XPlat Code Coverage\" -p:VSTestTestCaseFilter=FullyQualifiedName~Smoke")]
    public void MsBuildVSTestTestCaseFilterPropertyMakesCoverageBackfillUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("test filter");
    }

    [Fact]
    public void RunSettingsPathContainingCoverageTextWithoutCoverageCollectorDoesNotSelectCoverageReportSource()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"XPlat Code Coverage-{Guid.NewGuid():N}.runsettings");
        try
        {
            File.WriteAllText(filePath, "<RunSettings />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --settings \"{filePath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void NonVstestRunSettingsDataCollectorDoesNotSelectCoverageReportSource()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"external-coverage --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void DotnetCoverageGeneratedXmlReportDoesNotEnableBackfillAfterChildCommand()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated-cobertura.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output-format cobertura --output /tmp/generated-cobertura.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Theory]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml")]
    [InlineData("dotnet test /p:CollectCoverage=true;CoverletOutputFormat=cobertura;CoverletOutput=/tmp/generated.xml")]
    [InlineData("dotnet test -property:CollectCoverage=true -property:CoverletOutputFormat=cobertura -property:CoverletOutput=/tmp/generated.xml")]
    [InlineData("dotnet test --property:CollectCoverage=true --property:CoverletOutputFormat=cobertura --property:CoverletOutput=/tmp/generated.xml")]
    [InlineData("dotnet test --property CollectCoverage=true --property CoverletOutputFormat=cobertura --property CoverletOutput=/tmp/generated.xml")]
    [InlineData("dotnet test --property:CollectCoverage=true;CoverletOutputFormat=cobertura;CoverletOutput=/tmp/generated.xml")]
    [InlineData("dotnet test /p:CollectCoverage=true,CoverletOutputFormat=cobertura,CoverletOutput=/tmp/generated.xml")]
    [InlineData("dotnet test /p:CollectCoverage=true;CoverletOutputFormat=cobertura%2copencover;CoverletOutput=/tmp/generated.xml")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format xml")]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura")]
    [InlineData("dotnet-coverage collect \"dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura\"")]
    [InlineData("dotnet-coverage collect \"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml\"")]
    [InlineData("dotnet test -- --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura")]
    [InlineData("dotnet /tmp/Sample.Tests.dll --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura")]
    [InlineData("/tmp/Sample.Tests --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura")]
    [InlineData("dotnet exec /tmp/Sample.Tests.dll --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura")]
    [InlineData("dotnet run --project /tmp/Sample.Tests/Sample.Tests.csproj --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura")]
    [InlineData("dotnet run -p /tmp/Sample.Tests/Sample.Tests.csproj --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura")]
    [InlineData("dotnet test -p:TestingPlatformCommandLineArguments=\"--coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura\"")]
    public void GeneratedXmlReportCanBeBackfilledWhenFormatOptionExplicitlySelectsLineFormat(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet-coverage collect --output-format=xml --output /tmp/generated.xml")]
    [InlineData("dotnet-coverage collect --output-format=cobertura --output /tmp/generated.xml")]
    [InlineData("dotnet-coverage collect \"--output-format=cobertura\" \"--output=/tmp/generated.xml\"")]
    [InlineData("dotnet-coverage collect -f xml --output /tmp/generated.xml")]
    [InlineData("dotnet-coverage collect -f cobertura --output /tmp/generated.xml")]
    [InlineData("dotnet tool run dotnet-coverage -- collect --output /tmp/generated.xml --output-format cobertura dotnet test")]
    [InlineData("dotnet tool run --allow-roll-forward dotnet-coverage -- collect --output /tmp/generated.xml --output-format cobertura dotnet test")]
    [InlineData("dotnet tool run dotnet-coverage --allow-roll-forward collect --output /tmp/generated.xml --output-format cobertura dotnet test")]
    [InlineData("dotnet tool run dotnet-coverage --allow-roll-forward -- collect --output /tmp/generated.xml --output-format cobertura dotnet test")]
    [InlineData("dotnet-coverage collect dotnet --info --output /tmp/generated.xml --output-format cobertura")]
    [InlineData("dotnet-coverage collect dotnet --info -o /tmp/generated.xml -f cobertura")]
    public void DotnetCoverageGeneratedXmlReportCannotBeBackfilledWhenDotnetCoverageOwnsOutput(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void CoverletMsBuildExactFileOutputDoesNotMatchFormatSuffixedReportPath()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.cobertura.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.CanProcessExternalCoveragePathForCurrentCommand("/tmp/generated.cobertura.xml", out var processReason).Should().BeFalse();
        processReason.Should().Contain("written by the current coverage command");
        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("write the configured external coverage path");
    }

    [Theory]
    [InlineData("dotnet-coverage collect -s coverage.runsettings -id session-1 -if src/**/*.cs -l coverage.log -ll verbose -t 30 -o /tmp/generated.xml -f cobertura dotnet test")]
    [InlineData("dotnet-coverage collect --settings coverage.runsettings --session-id session-1 --include-files src/**/*.cs --log-file coverage.log --log-level verbose --timeout 30 --output /tmp/generated.xml --output-format cobertura dotnet test")]
    public void DotnetCoverageGeneratedXmlReportCannotBeBackfilledWhenValueAliasesPrecedeChildCommand(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Theory]
    [InlineData("dotnet-coverage collect --nologo -dco -sv -b -o /tmp/generated.xml -f cobertura dotnet test")]
    [InlineData("dotnet-coverage collect --disable-console-output --server-mode --background --output /tmp/generated.xml --output-format cobertura dotnet test")]
    public void DotnetCoverageGeneratedXmlReportCannotBeBackfilledWhenFlagsPrecedeOutputOptions(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Theory]
    [InlineData("dotnet-coverage collect -o /tmp/other.xml -o /tmp/generated.xml -f xml -f cobertura", false)]
    [InlineData("dotnet-coverage collect -o /tmp/generated.xml -o /tmp/other.xml -f xml -f cobertura", false)]
    [InlineData("dotnet-coverage collect -o /tmp/generated.xml -f cobertura -f xml", false)]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output= --output-format cobertura", false)]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml -o= --output-format cobertura", false)]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output --output-format cobertura", false)]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura --output-format=", false)]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml -f cobertura -f=", false)]
    [InlineData("dotnet-coverage collect --output /tmp/other.xml --output-format cobertura \"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml\"", false)]
    [InlineData("dotnet-coverage collect --output /tmp/other.xml --output-format cobertura \"dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura\"", false)]
    [InlineData("dotnet test --coverage --coverage-output /tmp/other.xml --coverage-output /tmp/generated.xml --coverage-output-format coverage --coverage-output-format cobertura", true)]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output /tmp/other.xml --coverage-output-format coverage --coverage-output-format cobertura", false)]
    [InlineData("dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura --coverage-output-format coverage", false)]
    [InlineData("dotnet test --coverlet --coverlet-output-format json --coverlet-output-format cobertura", false)]
    [InlineData("dotnet test -- --coverlet --coverlet-output-format json --coverlet-output-format cobertura", false)]
    [InlineData("dotnet exec /tmp/Sample.Tests.dll --coverlet --coverlet-output-format json --coverlet-output-format cobertura", false)]
    public void GeneratedXmlReportUsesDriverSpecificOutputAndFormatValues(string commandLine, bool expectedBackfillable)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().Be(expectedBackfillable);
        if (expectedBackfillable)
        {
            reason.Should().BeEmpty();
        }
        else
        {
            reason.Should().NotBeEmpty();
        }
    }

    [Theory]
    [InlineData("dotnet-coverage collect -o -f cobertura dotnet test")]
    [InlineData("dotnet-coverage collect --output-format cobertura -o")]
    [InlineData("dotnet-coverage collect --output-format cobertura --output=")]
    public void GeneratedXmlReportFailsClosedWhenDotnetCoverageOutputValueIsMissing(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("dotnet-coverage collect -f xml --output /tmp/generated.xml dotnet test")]
    [InlineData("dotnet tool run --allow-roll-forward dotnet-coverage -- collect -f xml --output /tmp/generated.xml dotnet test")]
    [InlineData("dotnet tool run dotnet-coverage --allow-roll-forward collect -f xml --output /tmp/generated.xml dotnet test")]
    public void DotnetCoverageCollectRequiresExternalCoveragePath(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("external XML report path");
    }

    [Fact]
    public void GeneratedXmlReportCannotUseExistingFileWhenDotnetCoverageChildCommandReferencesPath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var filePath = Path.Combine(workingDirectory, "generated.xml");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var coverageXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="1" />
                  </file>
                </report>
                """;
            File.WriteAllText(filePath, coverageXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet-coverage collect \"dotnet test --coverage --coverage-output {filePath} --coverage-output-format coverage\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("supported line-capable XML format");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void DotnetCoverageCollectResponseFileRequiresExternalCoveragePath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var responseFileContents =
                """
                -f
                cobertura
                --output
                generated.xml
                dotnet test
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "coverage.rsp"), responseFileContents);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect @coverage.rsp");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("external XML report path");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GeneratedXmlReportCannotBeBackfilledWhenDotnetCoverageOutputFormatIsUnsupported()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output-format opencover --output /tmp/generated.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void GeneratedXmlReportCannotBeBackfilledWhenDotnetCoverageUsesUnknownFormatOption()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --format opencover --output /tmp/generated.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void GeneratedXmlReportCannotMixDotnetCoverageOutputWithChildCoverletFormat()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void GeneratedXmlReportCannotUseChildLineCapableWriterWhenOuterDotnetCoverageWritesSamePathWithoutLineFormat()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml \"dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void GeneratedXmlReportCannotMixMicrosoftAndCoverletTestingPlatformWriters()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura --coverlet");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("unverifiable output paths");
    }

    [Fact]
    public void DotnetCoverageGeneratedXmlReportCannotUseOutputAfterChildCommand()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output-format cobertura dotnet test --output /tmp/generated.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void DotnetCoverageGeneratedXmlReportCannotUseOutputFormatAfterChildCommand()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml dotnet test --output-format cobertura");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void GeneratedXmlReportDoesNotTrustFictitiousCoverletTestingPlatformOutputOption()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --coverlet --coverlet-output /tmp/generated.xml --coverlet-output-format cobertura");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("supported coverage output option");
    }

    [Fact]
    public void GeneratedXmlReportCanBeBackfilledWhenMsBuildOutputPathContainsQuotedSeparator()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated;semi.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=\"/tmp/generated;semi.xml\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedXmlReportCanBeBackfilledWhenRunnerCommandEscapesQuotedMsBuildFormatList()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test \"/p:CollectCoverage=true\" \"/p:CoverletOutputFormat=\\\"cobertura,opencover\\\"\" \"/p:CoverletOutput=/tmp/generated.xml\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedXmlReportCanBeBackfilledWhenRunnerCommandEscapesQuotedMsBuildOutputPath()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated;semi.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test \"/p:CollectCoverage=true\" \"/p:CoverletOutputFormat=cobertura\" \"/p:CoverletOutput=\\\"/tmp/generated;semi.xml\\\"\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedXmlReportCanBeBackfilledWhenMsBuildOutputPathUsesEscapedSeparator()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated;semi.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true;CoverletOutput=/tmp/generated%3bsemi.xml;CoverletOutputFormat=cobertura");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedXmlReportCanBeBackfilledWhenMsBuildOutputFormatUsesEscapedComma()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json%2ccobertura /p:CoverletOutput=/tmp/generated.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:CoverletOutput=/tmp/other.xml")]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:CoverletOutput")]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:CoverletOutput=")]
    public void GeneratedXmlReportCannotBeBackfilledWhenLastMsBuildOutputPathDoesNotMatch(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Be("External coverage XML report generation must use a supported coverage output option for the configured external coverage path.");
    }

    [Theory]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutput=/tmp/generated.xml /p:CoverletOutputFormat=cobertura /p:CoverletOutputFormat=json")]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutput=/tmp/generated.xml /p:CoverletOutputFormat=cobertura /p:CoverletOutputFormat")]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutput=/tmp/generated.xml /p:CoverletOutputFormat=cobertura /p:CoverletOutputFormat=")]
    public void GeneratedXmlReportCannotBeBackfilledWhenLastMsBuildOutputFormatDoesNotMatch(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("must declare a supported line-capable XML format");
    }

    [Fact]
    public void GeneratedXmlReportCannotBeBackfilledWhenExactMsBuildOutputPathEndsAsNonXmlFormat()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutput=/tmp/generated.xml /p:CoverletOutputFormat=cobertura%2cjson");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("must declare a supported line-capable XML format");
    }

    [Fact]
    public void DotnetCoverageGeneratedXmlReportOutputPathUsesTestSessionWorkingDirectoryButCannotEnableBackfill()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(workingDirectory, "generated.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output-format cobertura --output generated.xml");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("after the instrumented test command exits");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("generated", "generated.cobertura.xml")]
    [InlineData("results/", "results/coverage.cobertura.xml")]
    public void GeneratedXmlReportOutputPathUsesExplicitProjectDirectoryForCoverletMsBuild(string coverletOutput, string expectedReportPath)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var projectPath = Path.Combine("tests", "Sample.Tests", "Sample.Tests.csproj");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(projectDirectory, expectedReportPath));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test {projectPath} /p:CollectCoverage=true /p:CoverletOutput={coverletOutput} /p:CoverletOutputFormat=cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GeneratedXmlReportOutputPathUsesExplicitDirectoryTargetForCoverletMsBuild()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var targetDirectory = Path.Combine("tests", "Sample.Tests");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "Sample.Tests.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(projectDirectory, "coverage.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test {targetDirectory} /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CoverletMsBuildLineThresholdDoesNotBlockDefaultProjectDirectoryCoverageReport(bool explicitProject)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = explicitProject ? Path.Combine(workingDirectory, "tests", "Sample.Tests") : workingDirectory;
        var projectPath = Path.Combine("tests", "Sample.Tests", "Sample.Tests.csproj");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "Sample.Tests.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(projectDirectory, "coverage.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            var target = explicitProject ? $" {projectPath}" : string.Empty;
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test{target} /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80 /p:ThresholdType=line");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildDefaultSolutionOutputsBlockBackfillWhenAnotherProjectWritesAnotherReport()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var firstProjectDirectory = Path.Combine(workingDirectory, "tests", "First.Tests");
        var secondProjectDirectory = Path.Combine(workingDirectory, "tests", "Second.Tests");
        var solutionPath = Path.Combine(workingDirectory, "Sample.sln");
        try
        {
            Directory.CreateDirectory(firstProjectDirectory);
            Directory.CreateDirectory(secondProjectDirectory);
            File.WriteAllText(Path.Combine(firstProjectDirectory, "First.Tests.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(secondProjectDirectory, "Second.Tests.csproj"), "<Project />");
            var solutionXml =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "First.Tests", "tests\First.Tests\First.Tests.csproj", "{7A8A7E82-582E-4C0B-8C15-1F4E3F2D53A1}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Second.Tests", "tests\Second.Tests\Second.Tests.csproj", "{55F74B94-D528-423C-858C-0344175C49D7}"
                EndProject
                Global
                EndGlobal
                """;
            File.WriteAllText(solutionPath, solutionXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(firstProjectDirectory, "coverage.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.sln /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("additional explicit coverage report paths");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("results/")]
    [InlineData("results/coverage")]
    public void CoverletMsBuildRelativeCommandLineSolutionOutputsBlockBackfillWhenAnotherProjectWritesAnotherReport(string coverletOutput)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var firstProjectDirectory = Path.Combine(workingDirectory, "tests", "First.Tests");
        var secondProjectDirectory = Path.Combine(workingDirectory, "tests", "Second.Tests");
        var solutionPath = Path.Combine(workingDirectory, "Sample.sln");
        try
        {
            Directory.CreateDirectory(firstProjectDirectory);
            Directory.CreateDirectory(secondProjectDirectory);
            File.WriteAllText(Path.Combine(firstProjectDirectory, "First.Tests.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(secondProjectDirectory, "Second.Tests.csproj"), "<Project />");
            var solutionXml =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "First.Tests", "tests\First.Tests\First.Tests.csproj", "{7A8A7E82-582E-4C0B-8C15-1F4E3F2D53A1}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Second.Tests", "tests\Second.Tests\Second.Tests.csproj", "{55F74B94-D528-423C-858C-0344175C49D7}"
                EndProject
                Global
                EndGlobal
                """;
            File.WriteAllText(solutionPath, solutionXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(firstProjectDirectory, "results", "coverage.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test Sample.sln /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput={coverletOutput}");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("additional explicit coverage report paths");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildRelativeCommandLineSingleProjectSolutionOutputCanBeBackfilled()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var solutionPath = Path.Combine(workingDirectory, "Sample.sln");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "Sample.Tests.csproj"), "<Project />");
            var solutionXml =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Sample.Tests", "tests\Sample.Tests\Sample.Tests.csproj", "{7A8A7E82-582E-4C0B-8C15-1F4E3F2D53A1}"
                EndProject
                Global
                EndGlobal
                """;
            File.WriteAllText(solutionPath, solutionXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(projectDirectory, "results", "coverage.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.sln /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=results/");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildRelativeProjectOutputsBlockBackfillWhenAnotherProjectWritesAnotherReport()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var firstProjectDirectory = Path.Combine(workingDirectory, "tests", "First.Tests");
        var secondProjectDirectory = Path.Combine(workingDirectory, "tests", "Second.Tests");
        var solutionPath = Path.Combine(workingDirectory, "Sample.sln");
        try
        {
            Directory.CreateDirectory(firstProjectDirectory);
            Directory.CreateDirectory(secondProjectDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                    <CoverletOutput>results/</CoverletOutput>
                    <CoverletOutputFormat>cobertura</CoverletOutputFormat>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(firstProjectDirectory, "First.Tests.csproj"), projectXml);
            File.WriteAllText(Path.Combine(secondProjectDirectory, "Second.Tests.csproj"), projectXml);
            var solutionXml =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "First.Tests", "tests\First.Tests\First.Tests.csproj", "{7A8A7E82-582E-4C0B-8C15-1F4E3F2D53A1}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Second.Tests", "tests\Second.Tests\Second.Tests.csproj", "{55F74B94-D528-423C-858C-0344175C49D7}"
                EndProject
                Global
                EndGlobal
                """;
            File.WriteAllText(solutionPath, solutionXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(firstProjectDirectory, "results", "coverage.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.sln");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("additional explicit coverage report paths");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildMixedExplicitAndDefaultSolutionOutputsBlockBackfillWhenAnotherProjectWritesAnotherReport()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var firstProjectDirectory = Path.Combine(workingDirectory, "tests", "First.Tests");
        var secondProjectDirectory = Path.Combine(workingDirectory, "tests", "Second.Tests");
        var solutionPath = Path.Combine(workingDirectory, "Sample.sln");
        try
        {
            Directory.CreateDirectory(firstProjectDirectory);
            Directory.CreateDirectory(secondProjectDirectory);
            var firstProjectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CoverletOutput>custom/first</CoverletOutput>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            var secondProjectXml =
                """
                <Project>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(firstProjectDirectory, "First.Tests.csproj"), firstProjectXml);
            File.WriteAllText(Path.Combine(secondProjectDirectory, "Second.Tests.csproj"), secondProjectXml);
            var solutionXml =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "First.Tests", "tests\First.Tests\First.Tests.csproj", "{7A8A7E82-582E-4C0B-8C15-1F4E3F2D53A1}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Second.Tests", "tests\Second.Tests\Second.Tests.csproj", "{55F74B94-D528-423C-858C-0344175C49D7}"
                EndProject
                Global
                EndGlobal
                """;
            File.WriteAllText(solutionPath, solutionXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(firstProjectDirectory, "custom", "first.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.sln /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("additional explicit coverage report paths");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildThresholdStillBlocksDefaultOutputWhenExternalPathIsOutsideProjectDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var projectPath = Path.Combine("tests", "Sample.Tests", "Sample.Tests.csproj");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(workingDirectory, "other", "coverage.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test {projectPath} /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80 /p:ThresholdType=line");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("threshold");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GeneratedXmlReportDetectsRelativePathReferenceAgainstWorkingDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(workingDirectory, "generated.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage --format cobertura --output generated.xml");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("supported coverage output option");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GeneratedXmlReportOutputPathComparisonUsesCurrentPlatform()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(workingDirectory, "Generated.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=generated");
            var settings = CreateSettings();
            var expectedBackfillable = FrameworkDescription.Instance.IsWindows();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().Be(expectedBackfillable);
            if (expectedBackfillable)
            {
                reason.Should().BeEmpty();
            }
            else
            {
                reason.Should().Contain("must write the configured external coverage path");
            }
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GeneratedXmlReportDoesNotTrustShortFormatOptionFromUnknownTools()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage -f cobertura --output /tmp/generated.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Theory]
    [InlineData("dotnet-coverage collect --output-format cobertura --output \"{0}\"")]
    [InlineData("dotnet test --coverage --coverage-output \"{0}\" --coverage-output-format cobertura")]
    [InlineData("dotnet test -p:TestingPlatformCommandLineArguments=\"--coverage --coverage-output {0} --coverage-output-format cobertura\"")]
    [InlineData("dotnet-coverage collect \"dotnet test --coverage --coverage-output {0} --coverage-output-format cobertura\"")]
    public void GeneratedXmlReportRequiresCommandToWriteConfiguredPath(string commandLineTemplate)
    {
        var existingCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-existing-{Guid.NewGuid():N}.xml");
        var otherCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-other-{Guid.NewGuid():N}.xml");
        try
        {
            var existingCoverageXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="0" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(existingCoveragePath, existingCoverageXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, existingCoveragePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, string.Format(commandLineTemplate, otherCoveragePath));
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("must write the configured external coverage path");
        }
        finally
        {
            File.Delete(existingCoveragePath);
            File.Delete(otherCoveragePath);
        }
    }

    [Fact]
    public void GeneratedXmlReportDoesNotTrustDotnetTestOutputAsCoverageOutput()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --output /tmp/generated.xml /p:CoverletOutputFormat=cobertura");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("supported coverage output option");
    }

    [Fact]
    public void DotnetCoverageFormatDetectionIncludesOptionsAfterChildCommand()
    {
        var command = CoverageBackfillCommandLine.Parse("dotnet-coverage collect --output out.xml dotnet test -f cobertura");

        command.DotnetCoverageShortFormatOptionValueContainsAny(["cobertura"]).Should().BeTrue();
        command.ContainsShortFrameworkOption().Should().BeFalse();
    }

    [Fact]
    public void DotnetCoverageShortFormatAfterChildCommandCannotBackfillGeneratedXml()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml dotnet test -f cobertura");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void DotnetCoverageUnsupportedShortFormatAfterChildCommandFailsClosed()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml dotnet test -f net8.0");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Fact]
    public void DotnetCoverageDoubleDashChildShortFrameworkOptionFailsClosed()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura dotnet test -- -f net8.0");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Fact]
    public void DotnetCoverageQuotedChildShortFrameworkOptionFailsClosed()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test --filter FullyQualifiedName~Smoke -f net8.0\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("filter");
    }

    [Theory]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet exec /tmp/Sample.Tests.dll --coverage --filter FullyQualifiedName~Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -p:TestingPlatformCommandLineArguments=--filter-trait=Category=Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-class Sample.Tests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-method Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-namespace Sample.Tests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-trait Category=Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-not-class Sample.Tests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-not-method Slow\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-not-namespace Sample.SlowTests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-not-trait Category=Slow\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-query /[category=smoke]\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test -- --filter-query:/[category=smoke]\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"nunit3-console /tmp/Sample.Tests.dll --where cat==Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"nunit3-console /tmp/Sample.Tests.dll --where=cat==Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"nunit3-console /tmp/Sample.Tests.dll --test Sample.Tests.SmokeTest\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"nunit3-console /tmp/Sample.Tests.dll --testlist /tmp/tests.txt\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"nunit3-console /tmp/Sample.Tests.dll -where cat==Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"nunit3-console /tmp/Sample.Tests.dll -test Sample.Tests.SmokeTest\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"nunit3-console /tmp/Sample.Tests.dll -testlist /tmp/tests.txt\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"xunit.console /tmp/Sample.Tests.dll -trait Category=Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"xunit.console.x64 /tmp/Sample.Tests.dll -trait Category=Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"xunit.console.x86 /tmp/Sample.Tests.dll -notrait Category=Slow\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -class Sample.Tests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -class=Sample.Tests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -method Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -namespace Sample.Tests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -trait Category=Smoke\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -notclass Sample.Tests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -notmethod Slow\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -notnamespace Sample.SlowTests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -notrait Category=Slow\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -notrait=Category=Slow\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -class- Sample.Tests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -method- Slow\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -namespace- Sample.SlowTests\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -trait- Category=Slow\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -filter /[category=smoke]\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet run -- -filter:/[category=smoke]\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet exec /tmp/Sample.Tests.dll --filter-uid test-uid\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet /tmp/Sample.Tests.dll --filter-uid test-uid\"")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"/tmp/Sample.Tests --treenode-filter /root/child\"")]
    public void DotnetCoverageQuotedTestingPlatformChildFilterFailsClosed(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("filter");
    }

    [Fact]
    public void DotnetCoverageQuotedChildUnexpandedResponseFileFailsClosed()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test @missing.rsp\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("response file");
    }

    [Fact]
    public void DotnetCoverageShortXmlFormatIsNotBackfillableBeforeTheReportExists()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect -f xml --output /tmp/generated.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("after the instrumented test command exits");
    }

    [Fact]
    public void GeneratedXmlReportDoesNotInferLineFormatFromOutputPath()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/cobertura.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage --output /tmp/cobertura.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("supported coverage output option");
    }

    [Fact]
    public void GeneratedXmlReportDoesNotTrustPreExistingOutputFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"coverage-{Guid.NewGuid():N}.xml");
        try
        {
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
            File.WriteAllText(filePath, coverageXml);

            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"external-coverage --output {filePath}");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("supported coverage output option");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GeneratedXmlReportDoesNotProcessStaleCurrentCommandOutputFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"coverage-{Guid.NewGuid():N}.xml");
        try
        {
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            var coverageXml =
                """
                <coverage line-rate="1" lines-valid="1" lines-covered="1">
                  <packages>
                    <package name="sample" line-rate="1">
                      <classes>
                        <class name="Calculator" filename="src/Calculator.cs" line-rate="1">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(filePath, coverageXml);

            var sessionStartTime = DateTimeOffset.UtcNow;
            var oldTimestamp = sessionStartTime.UtcDateTime.AddHours(-1);
            File.SetCreationTimeUtc(filePath, oldTimestamp);
            File.SetLastWriteTimeUtc(filePath, oldTimestamp);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --coverage --coverage-output {filePath} --coverage-output-format cobertura");

            DotnetCommon.CanProcessExternalCoveragePathForCurrentSession(filePath, sessionStartTime, out var reason).Should().BeFalse();
            reason.Should().Contain("current test session");
        }
        finally
        {
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ExistingExternalXmlDoesNotProcessStaleReportAfterActualItrSkip()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"coverage-{Guid.NewGuid():N}.xml");
        try
        {
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            var coverageXml =
                """
                <coverage line-rate="1" lines-valid="1" lines-covered="1">
                  <packages>
                    <package name="sample" line-rate="1">
                      <classes>
                        <class name="Calculator" filename="src/Calculator.cs" line-rate="1">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(filePath, coverageXml);

            var sessionStartTime = DateTimeOffset.UtcNow;
            var oldTimestamp = sessionStartTime.UtcDateTime.AddHours(-1);
            File.SetCreationTimeUtc(filePath, oldTimestamp);
            File.SetLastWriteTimeUtc(filePath, oldTimestamp);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, "1");

            DotnetCommon.CanProcessExternalCoveragePathForCurrentSession(filePath, sessionStartTime, out var reason).Should().BeFalse();
            reason.Should().Contain("current test session");
        }
        finally
        {
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("dotnet test --coverage")]
    [InlineData("dotnet test --coverage --coverage-output-format cobertura")]
    [InlineData("dotnet test --coverlet")]
    [InlineData("dotnet test --coverlet --coverlet-output-format cobertura")]
    [InlineData("dotnet-coverage collect --output /tmp/other.coverage")]
    [InlineData("dotnet-coverage collect --output-format cobertura")]
    [InlineData("dotnet test --collect:\"Code Coverage;Format=xml\"")]
    public void CurrentCoverageCommandDoesNotTrustExistingExternalXmlReportItDoesNotWrite(string commandLine)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"coverage-{Guid.NewGuid():N}.xml");
        try
        {
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
            File.WriteAllText(filePath, coverageXml);

            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().StartWith("External coverage XML report");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GeneratedXmlReportDetectsSlashColonPathReferenceBeforeTrustingExistingFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"coverage-{Guid.NewGuid():N}.xml");
        try
        {
            var coverageXml =
                """
                <coverage line-rate="1" lines-valid="1" lines-covered="1">
                  <packages>
                    <package name="sample" line-rate="1">
                      <classes>
                        <class name="Calculator" filename="src/Calculator.cs" line-rate="1">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(filePath, coverageXml);

            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, filePath);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"external-coverage /output:{filePath}");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("supported coverage output option");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GeneratedXmlReportDoesNotInferLineFormatFromUnrelatedArgument()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage --name cobertura --output /tmp/generated.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("supported coverage output option");
    }

    [Fact]
    public void UnknownGeneratedXmlReportIsUnsafeUntilLineCapabilityCanBeVerified()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated-coverage.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage --output /tmp/generated-coverage.xml");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("supported coverage output option");
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
        var runSettingsPath = WriteTempRunSettings("Code Coverage", dataCollectorFormat: "xml");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Theory]
    [InlineData("dotnet test --collect:\"Code Coverage;Format=xml\"")]
    [InlineData("dotnet test --collect:\"Code Coverage; Format = Xml\"")]
    [InlineData("dotnet test --collect:\"Code Coverage;Format=Cobertura\" --collect:\"Code Coverage;Format=xml\"")]
    [InlineData("dotnet test -p:VSTestCollect=\"Code Coverage;Format=xml\"")]
    public void MicrosoftCodeCoverageCollectFormatXmlCanBeBackfilledWithoutRunSettings(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet test --settings \"{0}\" --collect:\"Code Coverage;Format=Cobertura\"")]
    [InlineData("vstest Sample.dll /Settings:\"{0}\" /Collect:\"Code Coverage;Format=Cobertura\"")]
    [InlineData("dotnet test --settings \"{0}\" --collect:\"Code Coverage;Format=xml\" --collect:\"Code Coverage;Format=Cobertura\"")]
    [InlineData("dotnet test --settings \"{0}\" --collect:\"Code Coverage;Format=\"")]
    [InlineData("dotnet test --settings \"{0}\" --collect:\"Code Coverage;Format\"")]
    public void MicrosoftCodeCoverageCollectFormatOverridesRunSettingsXml(string commandLineTemplate)
    {
        var runSettingsPath = WriteTempRunSettings("Code Coverage", dataCollectorFormat: "xml");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, string.Format(commandLineTemplate, runSettingsPath));
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("XML output");
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Theory]
    [InlineData("vstest Sample.dll /EnableCodeCoverage /Settings:\"{0}\"")]
    [InlineData("dotnet /tmp/sdk/vstest.console.dll Sample.dll /EnableCodeCoverage /Settings:\"{0}\"")]
    [InlineData("dotnet test -p:VSTestCollect=\"Code Coverage\" -p:RunSettingsFilePath=\"{0}\"")]
    [InlineData("dotnet msbuild Sample.csproj -t:VSTest -p:VSTestCollect=\"Code Coverage\" -p:RunSettingsFilePath=\"{0}\"")]
    [InlineData("dotnet test -p:VSTestCollect=\"Code Coverage\" -p:VSTestSetting=\"{0}\"")]
    public void MicrosoftCodeCoverageCanUseVSTestInputsWithXmlRunSettings(string commandLineTemplate)
    {
        var runSettingsPath = WriteTempRunSettings("Code Coverage", dataCollectorFormat: "xml");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, string.Format(commandLineTemplate, runSettingsPath));
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Theory]
    [InlineData("XPlat Code Coverage", null)]
    [InlineData("Code Coverage", "xml")]
    public void RunSettingsDataCollectorRequiresCoverageBackfillAndWaitsForIpc(string friendlyName, string dataCollectorFormat)
    {
        var runSettingsPath = WriteTempRunSettings(friendlyName, dataCollectorFormat: dataCollectorFormat);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Theory]
    [InlineData("XPlat Code Coverage", null)]
    [InlineData("Code Coverage", "xml")]
    public void DisabledRunSettingsDataCollectorDoesNotSelectCoverageReportSource(string friendlyName, string dataCollectorFormat)
    {
        var runSettingsPath = WriteTempRunSettings(friendlyName, dataCollectorFormat: dataCollectorFormat, dataCollectorEnabled: "false");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeFalse();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void RunSettingsTestCaseFilterMakesCoverageBackfillUnsafe()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage", testCaseFilter: "FullyQualifiedName~Smoke");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("runsettings testcase filter");
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void RunSettingsTargetFrameworkMakesCoverageBackfillUnsafe()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage", targetFrameworkVersion: "net8.0");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("target-framework");
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void RunSettingsFilePathPropertySelectsCoverageReportSource()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test -p:RunSettingsFilePath=\"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void VSTestSettingPropertySelectsCoverageReportSource()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet msbuild Sample.csproj -t:VSTest -p:VSTestSetting=\"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void RunSettingsFilePathPropertyResolvesRelativeToSessionWorkingDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        var runSettingsPath = Path.Combine(workingDirectory, "coverage.runsettings");
        try
        {
            WriteRunSettings(runSettingsPath, "XPlat Code Coverage");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test -p:RunSettingsFilePath=coverage.runsettings");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("dotnet test {0} -p:RunSettingsFilePath=coverage.runsettings")]
    [InlineData("dotnet msbuild {0} -t:VSTest -p:VSTestSetting=coverage.runsettings")]
    public void RunSettingsMsBuildPropertyResolvesRelativeToExplicitProjectDirectory(string commandTemplate)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var projectPath = Path.Combine("tests", "Sample.Tests", "Sample.Tests.csproj");
        var runSettingsPath = Path.Combine(projectDirectory, "coverage.runsettings");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            WriteRunSettings(runSettingsPath, "XPlat Code Coverage", testCaseFilter: "FullyQualifiedName~Smoke");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, string.Format(commandTemplate, projectPath));
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("runsettings testcase filter");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Sample.slnx", "dotnet test {0} -p:RunSettingsFilePath=coverage.runsettings")]
    [InlineData("Sample.slnf", "dotnet test {0} -p:RunSettingsFilePath=coverage.runsettings")]
    [InlineData("Sample.sln", "dotnet test {0} -p:RunSettingsFilePath=coverage.runsettings")]
    [InlineData("Sample.slnx", "dotnet msbuild {0} -t:VSTest -p:VSTestSetting=coverage.runsettings")]
    [InlineData("Sample.slnf", "dotnet msbuild {0} -t:VSTest -p:VSTestSetting=coverage.runsettings")]
    [InlineData("Sample.sln", "dotnet msbuild {0} -t:VSTest -p:VSTestSetting=coverage.runsettings")]
    public void RunSettingsMsBuildPropertyResolvesRelativeToSolutionProjectDirectory(string solutionFileName, string commandTemplate)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var projectPath = solutionFileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ?
                              @"tests\Sample.Tests\Sample.Tests.csproj" :
                              "tests/Sample.Tests/Sample.Tests.csproj";
        var runSettingsPath = Path.Combine(projectDirectory, "coverage.runsettings");
        var solutionPath = Path.Combine(workingDirectory, solutionFileName);
        try
        {
            Directory.CreateDirectory(projectDirectory);
            WriteRunSettings(runSettingsPath, "XPlat Code Coverage", testCaseFilter: "FullyQualifiedName~Smoke");
            File.WriteAllText(solutionPath, CreateSolutionFile(solutionFileName, projectPath));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, string.Format(commandTemplate, solutionFileName));
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("runsettings testcase filter");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Sample.slnx", "dotnet test -p:RunSettingsFilePath=coverage.runsettings")]
    [InlineData("Sample.slnf", "dotnet test -p:RunSettingsFilePath=coverage.runsettings")]
    [InlineData("Sample.sln", "dotnet test -p:RunSettingsFilePath=coverage.runsettings")]
    [InlineData("Sample.slnx", "dotnet msbuild -t:VSTest -p:VSTestSetting=coverage.runsettings")]
    [InlineData("Sample.slnf", "dotnet msbuild -t:VSTest -p:VSTestSetting=coverage.runsettings")]
    [InlineData("Sample.sln", "dotnet msbuild -t:VSTest -p:VSTestSetting=coverage.runsettings")]
    public void RunSettingsMsBuildPropertyResolvesRelativeToImplicitSolutionProjectDirectory(string solutionFileName, string command)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var projectPath = solutionFileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ?
                              @"tests\Sample.Tests\Sample.Tests.csproj" :
                              "tests/Sample.Tests/Sample.Tests.csproj";
        var runSettingsPath = Path.Combine(projectDirectory, "coverage.runsettings");
        var solutionPath = Path.Combine(workingDirectory, solutionFileName);
        try
        {
            Directory.CreateDirectory(projectDirectory);
            WriteRunSettings(runSettingsPath, "XPlat Code Coverage", testCaseFilter: "FullyQualifiedName~Smoke");
            File.WriteAllText(solutionPath, CreateSolutionFile(solutionFileName, projectPath));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, command);
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("runsettings testcase filter");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void RunSettingsFilePathPropertyTestCaseFilterMakesCoverageBackfillUnsafe()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage", testCaseFilter: "FullyQualifiedName~Smoke");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test -p:RunSettingsFilePath=\"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("runsettings testcase filter");
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void VSTestSettingPropertyTestCaseFilterMakesCoverageBackfillUnsafe()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage", testCaseFilter: "FullyQualifiedName~Smoke");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"msbuild Sample.csproj -t:VSTest -p:VSTestSetting=\"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("runsettings testcase filter");
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void EmptyRunSettingsTestCaseFilterDoesNotMakeCoverageBackfillUnsafe()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage", testCaseFilter: "   ");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Fact]
    public void EmptyRunSettingsTargetFrameworkDoesNotMakeCoverageBackfillUnsafe()
    {
        var runSettingsPath = WriteTempRunSettings("XPlat Code Coverage", targetFrameworkVersion: "   ");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test --settings \"{runSettingsPath}\"");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(runSettingsPath);
        }
    }

    [Theory]
    [InlineData("dotnet test -- RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke")]
    [InlineData("dotnet test VSTestCLIRunSettings=RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke")]
    [InlineData("dotnet test /p:VSTestCLIRunSettings=RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke")]
    [InlineData("dotnet test -property:VSTestCLIRunSettings=RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke")]
    public void InlineRunSettingsTestCaseFilterMakesCoverageBackfillUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("runsettings testcase filter");
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -- RunConfiguration.TargetFrameworkVersion=net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" VSTestCLIRunSettings=RunConfiguration.TargetFrameworkVersion=net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" /p:VSTestCLIRunSettings=RunConfiguration.TargetFrameworkVersion=net8.0")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -property:VSTestCLIRunSettings=RunConfiguration.TargetFrameworkVersion=net8.0")]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test --collect \\\"XPlat Code Coverage\\\" -- RunConfiguration.TargetFrameworkVersion=net8.0\"")]
    public void InlineRunSettingsTargetFrameworkMakesCoverageBackfillUnsafe(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("target-framework");
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" -- RunConfiguration.TargetFrameworkVersion=net8.0 RunConfiguration.TargetFrameworkVersion=")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" VSTestCLIRunSettings=RunConfiguration.TargetFrameworkVersion=net8.0;RunConfiguration.TargetFrameworkVersion=")]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\" /p:VSTestCLIRunSettings=RunConfiguration.TargetFrameworkVersion=net8.0%3BRunConfiguration.TargetFrameworkVersion=")]
    public void RepeatedInlineRunSettingsTargetFrameworkUsesLastValue(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void EmptyInlineRunSettingsTestCaseFilterDoesNotMakeCoverageBackfillUnsafe()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test -- RunConfiguration.TestCaseFilter=");
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dotnet test -- RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke RunConfiguration.TestCaseFilter=")]
    [InlineData("dotnet test VSTestCLIRunSettings=RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke;RunConfiguration.TestCaseFilter=")]
    [InlineData("dotnet test /p:VSTestCLIRunSettings=RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke%3BRunConfiguration.TestCaseFilter=")]
    public void RepeatedInlineRunSettingsTestCaseFilterUsesLastValue(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings((ConfigurationKeys.CIVisibility.CodeCoveragePath, "/tmp/datadog-coverage"));

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void RepeatedInlineRunSettingsResultsDirectoryUsesLastValue()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var firstResultsDirectory = Path.Combine(workingDirectory, "first");
        var secondResultsDirectory = Path.Combine(workingDirectory, "second");
        var commandLine = $"dotnet test --collect \"XPlat Code Coverage\" -- RunConfiguration.ResultsDirectory={firstResultsDirectory} RunConfiguration.ResultsDirectory={secondResultsDirectory}";

        var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(commandLine, workingDirectory);

        resultsDirectories.Should().NotBeEmpty();
        resultsDirectories[0].Should().Be(secondResultsDirectory);
    }

    [Fact]
    public void VSTestSettingPropertyOverridesCommandLineSettingsFileResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var firstResultsDirectory = Path.Combine(workingDirectory, "first");
        var secondResultsDirectory = Path.Combine(workingDirectory, "second");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            WriteRunSettings(Path.Combine(workingDirectory, "first.runsettings"), "XPlat Code Coverage", resultsDirectory: firstResultsDirectory);
            WriteRunSettings(Path.Combine(workingDirectory, "second.runsettings"), "XPlat Code Coverage", resultsDirectory: secondResultsDirectory);
            var commandLine = "dotnet test --settings first.runsettings -p:VSTestSetting=second.runsettings";

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(commandLine, workingDirectory);

            resultsDirectories.Should().NotBeEmpty();
            resultsDirectories[0].Should().Be(secondResultsDirectory);
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void RelativeRunSettingsPathUsesTestSessionWorkingDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var runSettingsPath = Path.Combine(workingDirectory, "coverage.runsettings");
            WriteRunSettings(runSettingsPath, "XPlat Code Coverage");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --settings coverage.runsettings");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageIpcWaitsForSelectedToolEvenWhenItrSkippingIsDisabled()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        var settings = CreateSettingsWithSkipping(testsSkippingEnabled: false);

        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
    }

    [Theory]
    [InlineData("dotnet test --collect \"XPlat Code Coverage\"", true)]
    [InlineData("dotnet test /p:CollectCoverage=true", false)]
    [InlineData("dotnet-coverage collect \"dotnet test --collect \\\"XPlat Code Coverage\\\"\"", true)]
    [InlineData("dotnet-coverage collect \"dotnet test /p:CollectCoverage=true\"", false)]
    public void CoverletXmlFallbackWaitsOnlyForCoverletCollector(string commandLine, bool expectedWait)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverletXmlFallback(settings).Should().Be(expectedWait);
    }

    [Theory]
    [InlineData("dotnet-coverage collect \"dotnet test --collect \\\"XPlat Code Coverage\\\"\"")]
    [InlineData("dotnet-coverage collect \"dotnet test /p:CollectCoverage=true\"")]
    [InlineData("dotnet-coverage collect \"dotnet test --collect \\\"Code Coverage;Format=xml\\\"\"")]
    public void DotnetCoverageCollectWaitsForCoverageIpcWhenChildCommandUsesCoverageTool(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
    }

    [Fact]
    public void DotnetCoverageCollectDoesNotWaitForCoverageIpcWhenChildCommandDoesNotUseCoverageTool()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect \"dotnet test\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeFalse();
    }

    [Fact]
    public void CoverletCollectorThresholdPropertiesDoNotBlockBackfillWithoutExternalCoveragePath()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\" /p:Threshold=80 /p:ThresholdType=branch");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CoverletCollectorThresholdPropertiesDoNotBlockBackfillWithExternalCoveragePath()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/missing-coverage.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\" /p:Threshold=80 /p:ThresholdType=branch");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.ShouldWaitForCoverageIpc(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CoverletMsBuildLineThresholdDoesNotBlockGeneratedReportWrittenByCoverlet()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80 /p:ThresholdType=line");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CoverletMsBuildLineThresholdDoesNotBlockGeneratedReportWrittenByCoverletDirectoryOutput()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/results/coverage.cobertura.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/results/ /p:Threshold=80 /p:ThresholdType=line");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/tmp/results/coverage.cobertura.xml", "Cobertura")]
    [InlineData("/tmp/results/coverage.opencover.xml", "OpenCover")]
    public void CoverletMsBuildDirectoryOutputMatchesCoverageExtensionCaseInsensitively(string externalCoveragePath, string outputFormat)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, externalCoveragePath);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat={outputFormat} /p:CoverletOutput=/tmp/results/");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CoverletMsBuildLineThresholdDoesNotBlockGeneratedReportWrittenByCoverletNoExtensionOutput()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.cobertura.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated /p:Threshold=80 /p:ThresholdType=line");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CoverletMsBuildProjectOutputFormatDoesNotBlockGeneratedReportWrittenByCoverletNoExtensionOutput()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                    <CoverletOutput>/tmp/generated</CoverletOutput>
                    <CoverletOutputFormat>cobertura</CoverletOutputFormat>
                    <Threshold>80</Threshold>
                    <ThresholdType>line</ThresholdType>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.cobertura.xml");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("/tmp/results/coverage.net8.0.cobertura.xml", "/tmp/results/", "cobertura")]
    [InlineData("/tmp/results/coverage.net8.0.opencover.xml", "/tmp/results/", "opencover")]
    [InlineData("/tmp/generated.net8.0.cobertura.xml", "/tmp/generated", "cobertura")]
    [InlineData("/tmp/generated.net8.0.xml", "/tmp/generated.xml", "cobertura")]
    [InlineData("/tmp/generated.net472.xml", "/tmp/generated.xml", "cobertura")]
    [InlineData("/tmp/generated.netcoreapp3.1.xml", "/tmp/generated.xml", "cobertura")]
    [InlineData("/tmp/generated.netstandard2.0.xml", "/tmp/generated.xml", "cobertura")]
    [InlineData("/tmp/generated.net8.0-windows.xml", "/tmp/generated.xml", "cobertura")]
    public void CoverletMsBuildLineThresholdDoesNotBlockGeneratedReportWrittenByCoverletMultiTargetOutput(string externalCoveragePath, string coverletOutput, string outputFormat)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, externalCoveragePath);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat={outputFormat} /p:CoverletOutput={coverletOutput} /p:Threshold=80 /p:ThresholdType=line");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CoverletMsBuildNonLineThresholdBlocksBackfillWithoutExternalCoveragePath()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80 /p:ThresholdType=branch");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("threshold");
    }

    [Fact]
    public void CoverletMsBuildProjectNonLineThresholdBlocksBackfillWithoutExternalCoveragePath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                    <Threshold>80</Threshold>
                    <ThresholdType>branch</ThresholdType>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:CoverletOutputFormat=cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("threshold");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildDirectoryBuildPropsNonLineThresholdBlocksBackfillWithoutExternalCoveragePath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                """
                <Project>
                  <PropertyGroup>
                    <Threshold>80</Threshold>
                    <ThresholdType>branch</ThresholdType>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:CoverletOutputFormat=cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("threshold");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildImportedPropsNonLineThresholdBlocksBackfillWithoutExternalCoveragePath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            Directory.CreateDirectory(Path.Combine(workingDirectory, "build"));
            var importedPropsXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                    <Threshold>80</Threshold>
                    <ThresholdType>branch</ThresholdType>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <Import Project="build/coverage.props" />
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "build", "coverage.props"), importedPropsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:CoverletOutputFormat=cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("threshold");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildImportedPropsTestingPlatformFilterBlocksBackfill()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            Directory.CreateDirectory(Path.Combine(workingDirectory, "build"));
            var importedPropsXml =
                """
                <Project>
                  <PropertyGroup>
                    <TestingPlatformCommandLineArguments>--filter-trait Category=Smoke</TestingPlatformCommandLineArguments>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <Import Project="build/testing-platform.props" />
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "build", "testing-platform.props"), importedPropsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj --coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("test filter");
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverletMsBuildGlobalThresholdTypeOverridesDirectoryBuildPropsThresholdType()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var propsXml =
                """
                <Project>
                  <PropertyGroup>
                    <Threshold>80</Threshold>
                    <ThresholdType>branch</ThresholdType>
                  </PropertyGroup>
                </Project>
                """;
            var projectXml =
                """
                <Project>
                  <PropertyGroup>
                    <CollectCoverage>true</CollectCoverage>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Directory.Build.props"), propsXml);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), projectXml);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test Sample.Tests.csproj /p:CoverletOutputFormat=cobertura /p:ThresholdType=line");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void DotnetCoverageQuotedCoverletChildNonLineThresholdBlocksBackfillWithoutExternalCoveragePath()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect \"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80 /p:ThresholdType=branch\"");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("threshold");
    }

    [Theory]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80", false)]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80 /p:ThresholdType=branch", false)]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80 /p:ThresholdType=method", false)]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80 /p:ThresholdType=line,branch", false)]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80 /p:ThresholdType=branch /p:ThresholdType=line", true)]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80 /p:ThresholdType=line /p:ThresholdType=branch", false)]
    public void CoverletMsBuildThresholdRequiresLineOnlyThresholdType(string commandLine, bool expectedBackfillable)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().Be(expectedBackfillable);
        if (expectedBackfillable)
        {
            reason.Should().BeEmpty();
        }
        else
        {
            reason.Should().Contain("threshold");
        }
    }

    [Theory]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80\"", false)]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80 /p:ThresholdType=branch\"", false)]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80 /p:ThresholdType=branch\"", false)]
    [InlineData("dotnet-coverage collect --output /tmp/generated.xml --output-format cobertura \"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated.xml /p:Threshold=80 /p:ThresholdType=line\"", false)]
    public void DotnetCoverageQuotedCoverletChildThresholdRequiresLineOnlyThresholdType(string commandLine, bool expectedBackfillable)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().Be(expectedBackfillable);
        if (expectedBackfillable)
        {
            reason.Should().BeEmpty();
        }
        else
        {
            reason.Should().MatchRegex("threshold|after the instrumented test command exits");
        }
    }

    [Fact]
    public void CoverletMsBuildThresholdBlocksWhenNoExtensionOutputDoesNotWriteExternalReport()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/generated.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/generated /p:Threshold=80");
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("threshold");
    }

    [Fact]
    public void CoverletMsBuildThresholdBlocksExistingDirectoryOutputWithoutTrailingSeparator()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-output-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outputDirectory);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, Path.Combine(outputDirectory, "coverage.cobertura.xml"));
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=\"{outputDirectory}\" /p:Threshold=80");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
            reason.Should().Contain("threshold");
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput=/tmp/results/ /p:Threshold=80")]
    [InlineData("dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=/tmp/results/ /p:Threshold=80")]
    public void CoverletMsBuildThresholdStillBlocksWhenDirectoryOutputDoesNotWriteExternalReport(string commandLine)
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, "/tmp/other/coverage.cobertura.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
        var settings = CreateSettings();

        CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
        CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeFalse();
        reason.Should().Contain("threshold");
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

    [Theory]
    [InlineData("dotnet test /p:Threshold=80;Threshold=")]
    [InlineData("dotnet test /p:Threshold=80 /p:Threshold=")]
    [InlineData("dotnet test /p:Threshold=80 /p:Threshold")]
    [InlineData("dotnet test /p:ThresholdType=line /p:ThresholdType=")]
    [InlineData("dotnet test /p:ThresholdStat=total /p:ThresholdStat=")]
    public void EmptyEffectiveMsBuildThresholdDoesNotBlockPostCommandXmlBackfill(string commandLine)
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
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, commandLine);
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ThresholdTextInsideUnrelatedArgumentDoesNotDisableExternalXmlBackfill()
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
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "external-coverage --format cobertura --name threshold=80");
            var settings = CreateSettings();

            CoverageBackfillCapability.IsCoverageBackfillRequired(settings).Should().BeTrue();
            CoverageBackfillCapability.IsActiveCoverageModeBackfillable(settings, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
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
        CoverageBackfillCapability.ResetCommandLineCacheForTests();

        var allValues = new (string Key, string Value)[values.Length + 1];
        allValues[0] = (ConfigurationKeys.CIVisibility.TestsSkippingEnabled, testsSkippingEnabled ? "1" : "0");
        Array.Copy(values, 0, allValues, 1, values.Length);
        return new TestOptimizationSettings(CreateConfigurationSource(allValues), NullConfigurationTelemetry.Instance);
    }

    private static string WriteTempRunSettings(string friendlyName, string testCaseFilter = null, string dataCollectorFormat = null, string dataCollectorEnabled = null, string targetFrameworkVersion = null, string resultsDirectory = null)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}.runsettings");
        WriteRunSettings(filePath, friendlyName, testCaseFilter, dataCollectorFormat, dataCollectorEnabled, targetFrameworkVersion, resultsDirectory);
        return filePath;
    }

    private static void WriteRunSettings(string filePath, string friendlyName, string testCaseFilter = null, string dataCollectorFormat = null, string dataCollectorEnabled = null, string targetFrameworkVersion = null, string resultsDirectory = null)
    {
        var testCaseFilterConfiguration =
            testCaseFilter is null ?
                string.Empty :
                $"      <TestCaseFilter>{testCaseFilter}</TestCaseFilter>";
        var targetFrameworkConfiguration =
            targetFrameworkVersion is null ?
                string.Empty :
                $"      <TargetFrameworkVersion>{targetFrameworkVersion}</TargetFrameworkVersion>";
        var resultsDirectoryConfiguration =
            resultsDirectory is null ?
                string.Empty :
                $"      <ResultsDirectory>{resultsDirectory}</ResultsDirectory>";
        var runConfiguration =
            testCaseFilter is null && targetFrameworkVersion is null && resultsDirectory is null ?
                string.Empty :
                "<RunConfiguration>" + Environment.NewLine +
                testCaseFilterConfiguration + Environment.NewLine +
                targetFrameworkConfiguration + Environment.NewLine +
                resultsDirectoryConfiguration + Environment.NewLine +
                "    </RunConfiguration>";
        var dataCollectorConfiguration =
            dataCollectorFormat is null ?
                string.Empty :
                $$"""
                              <Configuration>
                                <Format>{{dataCollectorFormat}}</Format>
                              </Configuration>
                  """;
        var dataCollectorEnabledAttribute =
            dataCollectorEnabled is null ?
                string.Empty :
                $" enabled=\"{dataCollectorEnabled}\"";
        var contents =
            $$"""
              <RunSettings>
                {{runConfiguration}}
                <DataCollectionRunSettings>
                  <DataCollectors>
                    <DataCollector friendlyName="{{friendlyName}}"{{dataCollectorEnabledAttribute}}>
                    {{dataCollectorConfiguration}}
                    </DataCollector>
                  </DataCollectors>
                </DataCollectionRunSettings>
              </RunSettings>
              """;
        File.WriteAllText(filePath, contents);
    }

    private static string CreateSolutionFile(string solutionFileName, string projectPath)
    {
        if (solutionFileName.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                     {
                       "solution": {
                         "path": "Sample.sln",
                         "projects": [
                           "{{projectPath}}"
                         ]
                       }
                     }
                     """;
        }

        if (solutionFileName.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                     <Solution>
                       <Project Path="{{projectPath}}" />
                     </Solution>
                     """;
        }

        return $$"""
                 Microsoft Visual Studio Solution File, Format Version 12.00
                 # Visual Studio Version 17
                 Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Sample.Tests", "{{projectPath}}", "{7A8A7E82-582E-4C0B-8C15-1F4E3F2D53A1}"
                 EndProject
                 Global
                 EndGlobal
                 """;
    }
}
