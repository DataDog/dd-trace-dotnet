// <copyright file="CodeCoverageAdapterInjectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
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
    }

    [Theory]
    [MemberData(nameof(DotnetTestData))]
    public void InjectCodeCoverageCollectorToDotnetTest(string[] args, string[] expectedArgs)
    {
        var originalValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath);
        EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, "TempPath");

        using var scope = new AssertionScope();

        var modifiedArgs = (IEnumerable<string>)new List<string>(args);
        DotnetCommon.InjectCodeCoverageCollectorToDotnetTest(ref modifiedArgs);
        modifiedArgs.Should().Equal(expectedArgs);

        EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, originalValue);
    }

    [Theory]
    [MemberData(nameof(VsConsoleTestData))]
    public void InjectCodeCoverageCollectorToVsConsoleTest(string[] args, string[] expectedArgs)
    {
        var originalValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath);
        EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, "TempPath");

        using var scope = new AssertionScope();

        var modifiedArgs = new List<string>(args).ToArray();
        DotnetCommon.InjectCodeCoverageCollectorToVsConsoleTest(ref modifiedArgs);
        modifiedArgs.Should().Equal(expectedArgs);

        EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath, originalValue);
    }
}
