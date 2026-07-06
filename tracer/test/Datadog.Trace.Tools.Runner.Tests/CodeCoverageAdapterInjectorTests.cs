// <copyright file="CodeCoverageAdapterInjectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Util;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class CodeCoverageAdapterInjectorTests
{
    public static IEnumerable<object[]> DotnetTestData()
    {
        string[] emptyStringArray = [string.Empty];
        yield return [emptyStringArray, emptyStringArray.Concat(["-property:VSTestCollect=\"DatadogCoverage\"", "-property:VSTestTestAdapterPath=\"TempPath\""])];

        string[] spaceStringArray = [" "];
        yield return [spaceStringArray, spaceStringArray.Concat(["-property:VSTestCollect=\"DatadogCoverage\"", "-property:VSTestTestAdapterPath=\"TempPath\""])];

        string[] example1 =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e"
        ];
        string[] example1Expected =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e",
            "-property:VSTestCollect=\"DatadogCoverage\"",
            "-property:VSTestTestAdapterPath=\"TempPath\"",
        ];
        yield return [example1, example1Expected];

        string[] example2 =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:TargetFramework=net8.0",
            "-property:VSTestCLIRunSettings=\"my;prop\"",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e"
        ];
        string[] example2Expected =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:TargetFramework=net8.0",
            "-property:VSTestCLIRunSettings=\"my;prop\"",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e",
            "-property:VSTestCollect=\"DatadogCoverage\"",
            "-property:VSTestTestAdapterPath=\"TempPath\""
        ];
        yield return [example2, example2Expected];

        string[] example3 =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestCollect=\"MyCustomCollector\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e"
        ];
        string[] example3Expected =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestCollect=\"MyCustomCollector;DatadogCoverage\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e",
            "-property:VSTestTestAdapterPath=\"TempPath\"",
        ];
        yield return [example3, example3Expected];

        string[] example4 =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestTestAdapterPath=\"MyCustomCollectorPath\"",
            "-property:VSTestCollect=\"MyCustomCollector\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e"
        ];
        string[] example4Expected =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestTestAdapterPath=\"MyCustomCollectorPath;TempPath\"",
            "-property:VSTestCollect=\"MyCustomCollector;DatadogCoverage\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e"
        ];
        yield return [example4, example4Expected];

        string[] example5 =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestTestAdapterPath=\"TempPath\"",
            "-property:VSTestCollect=\"DatadogCoverage\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e"
        ];
        yield return [example5, example5];

        string[] example6 =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestCollect=\"MyCustomCollector\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestCLIRunSettings=\"MyProp=true;NewProp=false\"",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e"
        ];
        string[] example6Expected =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestCollect=\"MyCustomCollector;DatadogCoverage\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestCLIRunSettings=\"MyProp=true;NewProp=false\"",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e",
            "-property:VSTestTestAdapterPath=\"TempPath\"",
        ];
        yield return [example6, example6Expected];

        string[] example7 =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestTestAdapterPath=\"MyCustomCollectorPath\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e"
        ];
        string[] example7Expected =
        [
            "-target:VSTest",
            "-nodereuse:false",
            "-nologo",
            "-property:VSTestTestAdapterPath=\"MyCustomCollectorPath;TempPath\"",
            "-property:TargetFramework=net8.0",
            "-property:VSTestArtifactsProcessingMode=collect",
            "-property:VSTestSessionCorrelationId=44899_366ec916-8e2d-4410-af60-36466bee387e",
            "-property:VSTestCollect=\"DatadogCoverage\"",
        ];
        yield return [example7, example7Expected];

        string[] example8 =
        [
            "-target:VSTest",
            "-property:vstestcollect=\"MyCustomCollector\"",
            "-property:vstesttestadapterpath=\"MyCustomCollectorPath\""
        ];
        string[] example8Expected =
        [
            "-target:VSTest",
            "-property:VSTestCollect=\"MyCustomCollector;DatadogCoverage\"",
            "-property:VSTestTestAdapterPath=\"MyCustomCollectorPath;TempPath\""
        ];
        yield return [example8, example8Expected];

        string[] example9 =
        [
            "-target:VSTest",
            "-property:vstestcollect=\"DatadogCoverage\"",
            "-property:vstesttestadapterpath=\"TempPath\""
        ];
        yield return [example9, example9];
    }

    public static IEnumerable<object[]> VsConsoleTestData()
    {
        string[] emptyStringArray = [string.Empty];
        yield return [emptyStringArray, emptyStringArray.Concat(["/TestAdapterPath:\"TempPath\"", "/Collect:DatadogCoverage"])];

        string[] spaceStringArray = [" "];
        yield return [spaceStringArray, spaceStringArray.Concat(["/TestAdapterPath:\"TempPath\"", "/Collect:DatadogCoverage"])];

        string[] example1 = ["Samples.dll"];
        yield return [example1, example1.Concat(["/TestAdapterPath:\"TempPath\"", "/Collect:DatadogCoverage"])];

        string[] example2 = ["Samples.dll", "--", "DataCollectorSettings.Enabled=true"];
        string[] example2Expected = ["Samples.dll", "/TestAdapterPath:\"TempPath\"", "/Collect:DatadogCoverage", "--", "DataCollectorSettings.Enabled=true"];
        yield return [example2, example2Expected];

        string[] example3 = ["Samples.dll", "/Collect:MyCollector"];
        string[] example3Expected = ["Samples.dll", "/Collect:MyCollector", "/Collect:DatadogCoverage", "/TestAdapterPath:\"TempPath\""];
        yield return [example3, example3Expected];

        string[] example4 = ["Samples.dll", "/Collect:MyCollector", "/TestAdapterPath:c:\\temp"];
        string[] example4Expected = ["Samples.dll", "/Collect:MyCollector", "/Collect:DatadogCoverage", "/TestAdapterPath:c:\\temp", "/TestAdapterPath:\"TempPath\""];
        yield return [example4, example4Expected];

        string[] example5 = ["Samples.dll", "/Collect:MyCollector", "/TestAdapterPath:c:\\temp", "--", "DataCollectorSettings.Enabled=true"];
        string[] example5Expected = ["Samples.dll", "/Collect:MyCollector", "/Collect:DatadogCoverage", "/TestAdapterPath:c:\\temp", "/TestAdapterPath:\"TempPath\"", "--", "DataCollectorSettings.Enabled=true"];
        yield return [example5, example5Expected];

        string[] example6 = ["Samples.dll", "/collect:DatadogCoverage"];
        string[] example6Expected = ["Samples.dll", "/collect:DatadogCoverage", "/TestAdapterPath:\"TempPath\""];
        yield return [example6, example6Expected];

        string[] example7 = ["Samples.dll", "/collect:MyCollector", "/testadapterpath:c:\\temp"];
        string[] example7Expected = ["Samples.dll", "/collect:MyCollector", "/Collect:DatadogCoverage", "/testadapterpath:c:\\temp", "/TestAdapterPath:\"TempPath\""];
        yield return [example7, example7Expected];
    }

    /// <summary>
    /// Provides command lines that cover the VSTest result-directory forms used by Coverlet collector runs.
    /// </summary>
    /// <returns>Command line, working directory, and expected resolved results directory.</returns>
    public static IEnumerable<object[]> CoverletResultsDirectoryData()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "dd-coverlet-results");
        yield return [
            "dotnet test --collect \"XPlat Code Coverage\"",
            workingDirectory,
            Path.Combine(workingDirectory, "TestResults")
        ];
        yield return [
            "vstest sample.dll /Collect:\"XPlat Code Coverage\"",
            workingDirectory,
            Path.Combine(workingDirectory, "TestResults")
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" --results-directory ./coverage-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "coverage-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" --results-directory first-results --results-directory second-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" --ResultsDirectory:first-results --ResultsDirectory=second-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" --results-directory first-results --results-directory=",
            workingDirectory,
            Path.Combine(workingDirectory, "TestResults")
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" --results-directory first-results --results-directory",
            workingDirectory,
            Path.Combine(workingDirectory, "TestResults")
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" --results-directory first-results --results-directory --logger trx",
            workingDirectory,
            Path.Combine(workingDirectory, "TestResults")
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" --results-directory --logger trx --results-directory second-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" --results-directory first-results --results-directory second-results -- RunConfiguration.ResultsDirectory=",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
        yield return [
            "dotnet-coverage collect \"dotnet test --collect:\\\"XPlat Code Coverage\\\" --results-directory first-results --results-directory second-results\"",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
        yield return [
            "dotnet test \"--collect:XPlat Code Coverage\" \"--ResultsDirectory:/tmp/coverlet results\"",
            workingDirectory,
            Path.GetFullPath("/tmp/coverlet results")
        ];
        yield return [
            "dotnet test \"--collect:XPlat Code Coverage\" \"--ResultsDirectory=coverlet results\\\\\" --logger trx",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "coverlet results\\"))
        ];
        yield return [
            "dotnet test --collect \"XPlat Code Coverage;IncludeTestAssembly=true\" \"--ResultsDirectory=/tmp/coverlet results\"",
            workingDirectory,
            Path.GetFullPath("/tmp/coverlet results")
        ];
        yield return [
            "dotnet vstest sample.dll --collect:\"XPlat Code Coverage\" --ResultsDirectory:\"/tmp/coverlet results\"",
            workingDirectory,
            Path.GetFullPath("/tmp/coverlet results")
        ];
        yield return [
            "dotnet vstest sample.dll --collect:\"XPlat Code Coverage\" /ResultsDirectory:/tmp/coverlet-results",
            workingDirectory,
            Path.GetFullPath("/tmp/coverlet-results")
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" -- RunConfiguration.ResultsDirectory=inline-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "inline-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" -- RunConfiguration.ResultsDirectory=first-results RunConfiguration.ResultsDirectory=second-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" -- RunConfiguration.ResultsDirectory=\"inline results\"",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "inline results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" VSTestCLIRunSettings=RunConfiguration.ResultsDirectory=cli-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "cli-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" \"VSTestCLIRunSettings=RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke;RunConfiguration.ResultsDirectory=cli-results\"",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "cli-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" \"VSTestCLIRunSettings=RunConfiguration.ResultsDirectory=first-results;RunConfiguration.ResultsDirectory=second-results\"",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" /p:VSTestCLIRunSettings=RunConfiguration.TestCaseFilter=FullyQualifiedName~Smoke%3BRunConfiguration.ResultsDirectory=msbuild-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "msbuild-results"))
        ];
        yield return [
            "dotnet test --collect:\"XPlat Code Coverage\" /p:VSTestCLIRunSettings=RunConfiguration.ResultsDirectory=first-results%3BRunConfiguration.ResultsDirectory=second-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
        yield return [
            "msbuild Sample.Tests.csproj -t:VSTest -p:VSTestCollect=\"XPlat Code Coverage\" -p:VSTestResultsDirectory=msbuild-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "msbuild-results"))
        ];
        yield return [
            "dotnet msbuild Sample.Tests.csproj -t:VSTest -p:VSTestCollect=\"coverlet.collector\" -p:VSTestResultsDirectory=first-results -p:VSTestResultsDirectory=second-results",
            workingDirectory,
            Path.GetFullPath(Path.Combine(workingDirectory, "second-results"))
        ];
    }

    [Theory]
    [MemberData(nameof(DotnetTestData))]
    public void InjectCodeCoverageCollectorToDotnetTest(string[] args, string[] expectedArgs)
    {
        var originalValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath);
        try
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, "TempPath");

            using var scope = new AssertionScope();

            var modifiedArgs = (IEnumerable<string>)new List<string>(args);
            DotnetCommon.InjectCodeCoverageCollectorToDotnetTest(ref modifiedArgs);
            modifiedArgs.Should().Equal(expectedArgs);
        }
        finally
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, originalValue);
        }
    }

    [Theory]
    [InlineData("-property:TestingPlatformCommandLineArguments=--coverage")]
    [InlineData("-property:TestingPlatformCommandLineArguments=\"--coverage --coverage-output /tmp/generated.xml --coverage-output-format cobertura\"")]
    [InlineData("-property:TestingPlatformCommandLineArguments=--coverlet")]
    public void InjectCodeCoverageCollectorToDotnetTestDoesNotInjectVstestCollectorWhenTestingPlatformCoverageIsSelected(string testingPlatformArgument)
    {
        var originalValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath);
        try
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, "TempPath");
            var args = new[]
            {
                "-target:VSTest",
                testingPlatformArgument
            };

            var modifiedArgs = (IEnumerable<string>)new List<string>(args);
            DotnetCommon.InjectCodeCoverageCollectorToDotnetTest(ref modifiedArgs);

            modifiedArgs.Should().Equal(args);
        }
        finally
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, originalValue);
        }
    }

    [Fact]
    public void InjectCodeCoverageCollectorToDotnetTestPreservesExplicitDatadogCollectorWhenTestingPlatformCoverageIsSelected()
    {
        var originalValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath);
        try
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, "TempPath");
            var args = new[]
            {
                "-target:VSTest",
                "-property:TestingPlatformCommandLineArguments=--coverage",
                "-property:VSTestCollect=\"DatadogCoverage\""
            };

            var modifiedArgs = (IEnumerable<string>)new List<string>(args);
            DotnetCommon.InjectCodeCoverageCollectorToDotnetTest(ref modifiedArgs);

            modifiedArgs.Should().Equal(
                "-target:VSTest",
                "-property:TestingPlatformCommandLineArguments=--coverage",
                "-property:VSTestCollect=\"DatadogCoverage\"",
                "-property:VSTestTestAdapterPath=\"TempPath\"");
        }
        finally
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, originalValue);
        }
    }

    [Theory]
    [MemberData(nameof(VsConsoleTestData))]
    public void InjectCodeCoverageCollectorToVsConsoleTest(string[] args, string[] expectedArgs)
    {
        var originalValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath);
        try
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, "TempPath");

            using var scope = new AssertionScope();

            var modifiedArgs = new List<string>(args).ToArray();
            DotnetCommon.InjectCodeCoverageCollectorToVsConsoleTest(ref modifiedArgs);
            modifiedArgs.Should().Equal(expectedArgs);
        }
        finally
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, originalValue);
        }
    }

    /// <summary>
    /// Verifies that Coverlet collector result directories are resolved from common dotnet test and vstest command lines.
    /// </summary>
    /// <param name="commandLine">Command line to parse.</param>
    /// <param name="workingDirectory">Command working directory used for relative paths.</param>
    /// <param name="expectedPath">Expected absolute results directory.</param>
    [Theory]
    [MemberData(nameof(CoverletResultsDirectoryData))]
    public void TryGetCoverletCollectorResultsDirectory(string commandLine, string workingDirectory, string expectedPath)
    {
        DotnetCommon.TryGetCoverletCollectorResultsDirectory(commandLine, workingDirectory, out var resultsDirectory)
                    .Should()
                    .BeTrue();
        resultsDirectory.Should().Be(expectedPath);
    }

    /// <summary>
    /// Verifies that Coverlet collector fallback honors runsettings result-directory configuration when no command-line result directory is present.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryReadsRunSettingsResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var runSettingsPath = Path.Combine(workingDirectory, "coverlet.runsettings");
            var runSettingsContents =
                """
                <RunSettings>
                  <RunConfiguration>
                    <ResultsDirectory>custom-results</ResultsDirectory>
                  </RunConfiguration>
                </RunSettings>
                """;
            File.WriteAllText(runSettingsPath, runSettingsContents);

            DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                           "dotnet test --collect:\"XPlat Code Coverage\" --settings coverlet.runsettings",
                           workingDirectory,
                           out var resultsDirectory)
                       .Should()
                       .BeTrue();

            resultsDirectory.Should().Be(Path.Combine(workingDirectory, "custom-results"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that relative runsettings result directories are resolved from the runsettings file location.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryResolvesRunSettingsResultsDirectoryFromRunSettingsFileDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            var settingsDirectory = Path.Combine(workingDirectory, "settings");
            Directory.CreateDirectory(settingsDirectory);
            var runSettingsPath = Path.Combine(settingsDirectory, "coverlet.runsettings");
            var runSettingsContents =
                """
                <RunSettings>
                  <RunConfiguration>
                    <ResultsDirectory>custom-results</ResultsDirectory>
                  </RunConfiguration>
                </RunSettings>
                """;
            File.WriteAllText(runSettingsPath, runSettingsContents);

            DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                           "dotnet test --collect:\"XPlat Code Coverage\" --settings settings/coverlet.runsettings",
                           workingDirectory,
                           out var resultsDirectory)
                       .Should()
                       .BeTrue();

            resultsDirectory.Should().Be(Path.Combine(settingsDirectory, "custom-results"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that runsettings can select the Coverlet collector and configure its result directory.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryDetectsCollectorFromRunSettings()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var runSettingsPath = Path.Combine(workingDirectory, "coverlet.runsettings");
            var runSettingsContents =
                """
                <RunSettings>
                  <RunConfiguration>
                    <ResultsDirectory>custom-results</ResultsDirectory>
                  </RunConfiguration>
                  <DataCollectionRunSettings>
                    <DataCollectors>
                      <DataCollector friendlyName="XPlat Code Coverage" />
                    </DataCollectors>
                  </DataCollectionRunSettings>
                </RunSettings>
                """;
            File.WriteAllText(runSettingsPath, runSettingsContents);

            DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                           "dotnet test --settings coverlet.runsettings",
                           workingDirectory,
                           out var resultsDirectory)
                       .Should()
                       .BeTrue();

            resultsDirectory.Should().Be(Path.Combine(workingDirectory, "custom-results"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that a disabled Coverlet collector in runsettings does not enable result-directory discovery.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryIgnoresDisabledCollectorFromRunSettings()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var runSettingsPath = Path.Combine(workingDirectory, "coverlet.runsettings");
            var runSettingsContents =
                """
                <RunSettings>
                  <RunConfiguration>
                    <ResultsDirectory>custom-results</ResultsDirectory>
                  </RunConfiguration>
                  <DataCollectionRunSettings>
                    <DataCollectors>
                      <DataCollector friendlyName="XPlat Code Coverage" enabled="false" />
                    </DataCollectors>
                  </DataCollectionRunSettings>
                </RunSettings>
                """;
            File.WriteAllText(runSettingsPath, runSettingsContents);

            DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                           "dotnet test --settings coverlet.runsettings",
                           workingDirectory,
                           out _)
                       .Should()
                       .BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that inline runsettings result-directory configuration wins over an explicit VSTest result-directory switch.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryPrefersInlineRunSettingsResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "dd-coverlet-results");

        DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                       "dotnet test --collect:\"XPlat Code Coverage\" --results-directory explicit-results -- RunConfiguration.ResultsDirectory=inline-results",
                       workingDirectory,
                       out var resultsDirectory)
                   .Should()
                   .BeTrue();

        resultsDirectory.Should().Be(Path.GetFullPath(Path.Combine(workingDirectory, "inline-results")));
    }

    /// <summary>
    /// Verifies that the last inline runsettings value wins and an empty final value falls through to explicit VSTest switches.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryFallsBackWhenLastInlineRunSettingsResultsDirectoryIsEmpty()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "dd-coverlet-results");

        DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                       "dotnet test --collect:\"XPlat Code Coverage\" --results-directory explicit-results -- RunConfiguration.ResultsDirectory=inline-results RunConfiguration.ResultsDirectory=",
                       workingDirectory,
                       out var resultsDirectory)
                   .Should()
                   .BeTrue();

        resultsDirectory.Should().Be(Path.GetFullPath(Path.Combine(workingDirectory, "explicit-results")));
    }

    /// <summary>
    /// Verifies that commented response-file switches do not participate in result-directory discovery.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryIgnoresCommentedResponseFileSwitches()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var responseFileContents =
                """
                # --ResultsDirectory:comment-results
                --collect
                XPlat Code Coverage
                --ResultsDirectory:actual-results
                """;

            File.WriteAllText(
                Path.Combine(workingDirectory, "coverage.rsp"),
                responseFileContents);

            DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                           "dotnet test @coverage.rsp",
                           workingDirectory,
                           out var resultsDirectory)
                       .Should()
                       .BeTrue();

            resultsDirectory.Should().Be(Path.GetFullPath(Path.Combine(workingDirectory, "actual-results")));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that explicit VSTest result-directory switches win over file runsettings result directories.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryPrefersExplicitResultDirectoryOverRunSettingsFile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var runSettingsPath = Path.Combine(workingDirectory, "coverlet.runsettings");
            var runSettingsContents =
                """
                <RunSettings>
                  <RunConfiguration>
                    <ResultsDirectory>file-results</ResultsDirectory>
                  </RunConfiguration>
                </RunSettings>
                """;
            File.WriteAllText(runSettingsPath, runSettingsContents);

            DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                           "dotnet test --collect:\"XPlat Code Coverage\" --settings coverlet.runsettings --results-directory explicit-results",
                           workingDirectory,
                           out var resultsDirectory)
                       .Should()
                       .BeTrue();

            resultsDirectory.Should().Be(Path.GetFullPath(Path.Combine(workingDirectory, "explicit-results")));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that result-directory resolution is disabled when the command does not enable the Coverlet collector.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryReturnsFalseWhenCoverletIsNotEnabled()
    {
        DotnetCommon.TryGetCoverletCollectorResultsDirectory("dotnet test", "/tmp/work", out _)
                    .Should()
                    .BeFalse();
        DotnetCommon.TryGetCoverletCollectorResultsDirectory("dotnet test -- RunConfiguration.ResultsDirectory=inline-results", "/tmp/work", out _)
                    .Should()
                    .BeFalse();
    }

    /// <summary>
    /// Verifies that Coverlet-looking text in unrelated arguments does not enable the XML fallback.
    /// </summary>
    [Theory]
    [InlineData("dotnet test \"/tmp/coverlet.collector/Sample.Tests.dll\" --results-directory ./coverage-results")]
    [InlineData("dotnet test --filter \"DisplayName~XPlat Code Coverage\" --results-directory ./coverage-results")]
    [InlineData("dotnet test --results-directory \"/tmp/coverlet.collector-results\"")]
    [InlineData("dotnet test /p:CollectCoverage=true --results-directory ./coverage-results")]
    [InlineData("dotnet test -- RunConfiguration.ResultsDirectory=inline-results")]
    [InlineData("external-coverage --collect \"XPlat Code Coverage\" --results-directory ./coverage-results")]
    public void TryGetCoverletCollectorResultsDirectoryReturnsFalseWhenCoverageTextIsNotCollectorSelection(string commandLine)
    {
        DotnetCommon.TryGetCoverletCollectorResultsDirectory(commandLine, "/tmp/work", out _)
                    .Should()
                    .BeFalse();
    }

    /// <summary>
    /// Verifies that a runsettings path containing coverage words is not treated as Coverlet collector activation.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryReturnsFalseWhenRunSettingsPathMentionsCoverageButCollectorIsAbsent()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var runSettingsPath = Path.Combine(workingDirectory, "XPlat Code Coverage.runsettings");
            File.WriteAllText(runSettingsPath, "<RunSettings />");

            DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                           "dotnet test --settings \"XPlat Code Coverage.runsettings\" --results-directory ./coverage-results",
                           workingDirectory,
                           out _)
                       .Should()
                       .BeFalse();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that an explicit dotnet test project path adds VSTest's project-local default TestResults directory.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesExplicitProjectDefaultResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var projectPath = Path.Combine("tests", "Sample.Tests", "Sample.Tests.csproj");
        try
        {
            Directory.CreateDirectory(projectDirectory);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                $"dotnet test {projectPath} --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(projectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that explicit MSBuild VSTest project paths add VSTest's project-local default TestResults directory.
    /// </summary>
    [Theory]
    [InlineData("msbuild {0} -t:VSTest -p:VSTestCollect=\"XPlat Code Coverage\"")]
    [InlineData("dotnet msbuild {0} -t:Restore;VSTest -p:VSTestCollect=\"XPlat Code Coverage\"")]
    [InlineData("dotnet msbuild {0} -target:VSTest -p:VSTestCollect=\"coverlet.collector\"")]
    public void GetCoverletCollectorResultsDirectoriesIncludesMsBuildProjectDefaultResultsDirectory(string commandLineFormat)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var projectPath = Path.Combine("tests", "Sample.Tests", "Sample.Tests.csproj");
        try
        {
            Directory.CreateDirectory(projectDirectory);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                string.Format(commandLineFormat, projectPath),
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(projectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that an explicit dotnet test directory target adds VSTest's target-local default TestResults directory.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesExplicitDirectoryDefaultResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var targetDirectory = Path.Combine("tests", "Sample.Tests");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "Sample.Tests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                $"dotnet test {targetDirectory} --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(projectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that dotnet test solution arguments add default TestResults directories for SDK-style projects in the solution.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesSlnProjectDefaultResultsDirectories()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var solutionDirectory = Path.Combine(workingDirectory, "solutions");
        var csharpProjectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        var fsharpProjectDirectory = Path.Combine(workingDirectory, "tests", "Sample.FSharp.Tests");
        var visualBasicProjectDirectory = Path.Combine(workingDirectory, "tests", "Sample.VisualBasic.Tests");
        try
        {
            Directory.CreateDirectory(solutionDirectory);
            Directory.CreateDirectory(csharpProjectDirectory);
            Directory.CreateDirectory(fsharpProjectDirectory);
            Directory.CreateDirectory(visualBasicProjectDirectory);
            var solutionPath = Path.Combine(solutionDirectory, "Sample.sln");
            var solutionContents =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Sample.Tests", "..\tests\Sample.Tests\Sample.Tests.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{F2A71F9B-5D33-465A-A702-920D77279786}") = "Sample.FSharp.Tests", "..\tests\Sample.FSharp.Tests\Sample.FSharp.Tests.fsproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Project("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}") = "Sample.VisualBasic.Tests", "..\tests\Sample.VisualBasic.Tests\Sample.VisualBasic.Tests.vbproj", "{33333333-3333-3333-3333-333333333333}"
                EndProject
                Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "Native", "..\native\Native.vcxproj", "{44444444-4444-4444-4444-444444444444}"
                EndProject
                """;
            File.WriteAllText(solutionPath, solutionContents);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet test solutions/Sample.sln --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(csharpProjectDirectory, "TestResults"),
                Path.Combine(fsharpProjectDirectory, "TestResults"),
                Path.Combine(visualBasicProjectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
            resultsDirectories.Should().NotContain(Path.Combine(workingDirectory, "native", "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that dotnet test .slnx arguments add project-local default TestResults directories.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesSlnxProjectDefaultResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var solutionDirectory = Path.Combine(workingDirectory, "solutions");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        try
        {
            Directory.CreateDirectory(solutionDirectory);
            Directory.CreateDirectory(projectDirectory);
            var solutionContents =
                """
                <Solution>
                  <Project Path="../tests/Sample.Tests/Sample.Tests.csproj" />
                  <Project Path="../native/Native.vcxproj" />
                </Solution>
                """;
            File.WriteAllText(Path.Combine(solutionDirectory, "Sample.slnx"), solutionContents);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet test solutions/Sample.slnx --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(projectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
            resultsDirectories.Should().NotContain(Path.Combine(workingDirectory, "native", "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that dotnet test .slnf arguments add project-local default TestResults directories.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesSlnfProjectDefaultResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var solutionDirectory = Path.Combine(workingDirectory, "solutions");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        try
        {
            Directory.CreateDirectory(solutionDirectory);
            Directory.CreateDirectory(projectDirectory);
            var solutionContents =
                """
                {
                  "solution": {
                    "path": "Sample.sln",
                    "projects": [
                      "../tests/Sample.Tests/Sample.Tests.csproj",
                      "../native/Native.vcxproj"
                    ]
                  }
                }
                """;
            File.WriteAllText(Path.Combine(solutionDirectory, "Sample.slnf"), solutionContents);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet test solutions/Sample.slnf --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(projectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
            resultsDirectories.Should().NotContain(Path.Combine(workingDirectory, "native", "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that implicit MSBuild VSTest commands add project-local default TestResults directories from the implicit solution in the working directory.
    /// </summary>
    [Theory]
    [InlineData("msbuild -t:VSTest -p:VSTestCollect=\"XPlat Code Coverage\"")]
    [InlineData("dotnet msbuild -target:VSTest -p:VSTestCollect=\"coverlet.collector\"")]
    public void GetCoverletCollectorResultsDirectoriesIncludesImplicitMsBuildSlnxProjectDefaultResultsDirectory(string commandLine)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            var solutionContents =
                """
                <Solution>
                  <Project Path="tests/Sample.Tests/Sample.Tests.csproj" />
                  <Project Path="native/Native.vcxproj" />
                </Solution>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.slnx"), solutionContents);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                commandLine,
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(projectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
            resultsDirectories.Should().NotContain(Path.Combine(workingDirectory, "native", "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that bare dotnet test adds project-local default TestResults directories from the implicit solution in the working directory.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesImplicitSlnxProjectDefaultResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            var solutionContents =
                """
                <Solution>
                  <Project Path="tests/Sample.Tests/Sample.Tests.csproj" />
                  <Project Path="native/Native.vcxproj" />
                </Solution>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.slnx"), solutionContents);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet test --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(projectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
            resultsDirectories.Should().NotContain(Path.Combine(workingDirectory, "native", "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that bare dotnet test adds project-local default TestResults directories from the implicit solution filter in the working directory.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesImplicitSlnfProjectDefaultResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workingDirectory, "tests", "Sample.Tests");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            var solutionContents =
                """
                {
                  "solution": {
                    "path": "Sample.sln",
                    "projects": [
                      "tests/Sample.Tests/Sample.Tests.csproj",
                      "native/Native.vcxproj"
                    ]
                  }
                }
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.slnf"), solutionContents);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet test --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(projectDirectory, "TestResults"),
                Path.Combine(workingDirectory, "TestResults"));
            resultsDirectories.Should().NotContain(Path.Combine(workingDirectory, "native", "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that bare dotnet test adds the project-local default TestResults directory when the working directory is the project directory.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesImplicitProjectDefaultResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.Tests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet test --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().Contain(Path.Combine(workingDirectory, "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that explicit result-directory switches keep precedence over solution-derived default directories.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesPrefersExplicitResultsDirectoryOverSolutionDefaults()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var solutionContents =
                """
                <Solution>
                  <Project Path="tests/Sample.Tests/Sample.Tests.csproj" />
                </Solution>
                """;
            File.WriteAllText(Path.Combine(workingDirectory, "Sample.slnx"), solutionContents);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet test Sample.slnx --collect:\"XPlat Code Coverage\" --results-directory explicit-results",
                workingDirectory);

            resultsDirectories.Should().Equal(Path.Combine(workingDirectory, "explicit-results"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that dotnet-coverage wrapper commands still expose Coverlet collector result directories from the quoted child dotnet test command.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesDotnetCoverageCollectChildCommandResultsDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet-coverage collect \"dotnet test --collect:\\\"XPlat Code Coverage\\\" --results-directory child-results\"",
                workingDirectory);

            resultsDirectories.Should().Equal(Path.Combine(workingDirectory, "child-results"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that missing or malformed solution files fail closed to the existing working-directory fallback.
    /// </summary>
    [Theory]
    [InlineData("Missing.sln")]
    [InlineData("Malformed.slnf")]
    [InlineData("Malformed.slnx")]
    public void GetCoverletCollectorResultsDirectoriesFallsBackWhenSolutionCannotBeRead(string solutionFileName)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            if (solutionFileName.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(Path.Combine(workingDirectory, solutionFileName), "<Solution><Project Path=\"tests/Sample.Tests/Sample.Tests.csproj\"");
            }
            else if (solutionFileName.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(Path.Combine(workingDirectory, solutionFileName), """{"solution":{"projects":["tests/Sample.Tests/Sample.Tests.csproj"]""");
            }

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                $"dotnet test {solutionFileName} --collect:\"XPlat Code Coverage\"",
                workingDirectory);

            resultsDirectories.Should().Equal(Path.Combine(workingDirectory, "TestResults"));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that metadata-only Windows working directories do not hide Coverlet's default TestResults directory on non-Windows testhosts.
    /// </summary>
    [Fact]
    public void TryGetCoverletCollectorResultsDirectoryUsesCurrentDirectoryForForeignWindowsWorkingDirectory()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            return;
        }

        var originalWorkingDirectory = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        try
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, @"C:\evp_demo\working_directory");
            DotnetCommon.TryGetCoverletCollectorResultsDirectory(
                           "vstest sample.dll --collect:\"XPlat Code Coverage\"",
                           @"C:\evp_demo\working_directory",
                           out var resultsDirectory)
                       .Should()
                       .BeTrue();

            resultsDirectory.Should().Be(Path.Combine(Environment.CurrentDirectory, "TestResults"));
        }
        finally
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, originalWorkingDirectory);
        }
    }

    /// <summary>
    /// Verifies that the Coverlet XML fallback also searches the run-folder base when the public working directory is metadata-only.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesRunFolderBaseForForeignWindowsWorkingDirectory()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            return;
        }

        var originalWorkingDirectory = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var originalRunFolder = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, @"C:\evp_demo\working_directory");
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(runFolderBaseDirectory, ".dd", "test-run"));

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "vstest sample.dll --collect:\"XPlat Code Coverage\"",
                @"C:\evp_demo\working_directory");

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(Environment.CurrentDirectory, "TestResults"),
                Path.Combine(runFolderBaseDirectory, "TestResults"));
        }
        finally
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, originalWorkingDirectory);
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, originalRunFolder);
        }
    }

    /// <summary>
    /// Verifies that the run-folder base also gets VSTest's project-local default TestResults directory.
    /// </summary>
    [Fact]
    public void GetCoverletCollectorResultsDirectoriesIncludesRunFolderProjectDefaultResultsDirectoryForForeignWindowsWorkingDirectory()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            return;
        }

        var originalWorkingDirectory = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var originalRunFolder = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-coverlet-results-{Guid.NewGuid():N}");
        try
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, @"C:\evp_demo\working_directory");
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(runFolderBaseDirectory, ".dd", "test-run"));

            var resultsDirectories = DotnetCommon.GetCoverletCollectorResultsDirectories(
                "dotnet test tests/Sample.Tests/Sample.Tests.csproj --collect:\"XPlat Code Coverage\"",
                @"C:\evp_demo\working_directory");

            resultsDirectories.Should().ContainInOrder(
                Path.Combine(Environment.CurrentDirectory, "tests", "Sample.Tests", "TestResults"),
                Path.Combine(Environment.CurrentDirectory, "TestResults"),
                Path.Combine(runFolderBaseDirectory, "tests", "Sample.Tests", "TestResults"),
                Path.Combine(runFolderBaseDirectory, "TestResults"));
        }
        finally
        {
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, originalWorkingDirectory);
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, originalRunFolder);
        }
    }
}
