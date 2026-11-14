// <copyright file="TestingPlatformRunnerDetectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class TestingPlatformRunnerDetectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd-mtp-detector-" + Path.GetRandomFileName());

    public TestingPlatformRunnerDetectorTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{ "test": { "runner": "Microsoft.Testing.Platform" } }""", true)]
    [InlineData("""{ "test": { "runner": "microsoft.testing.platform" } }""", true)]
    [InlineData("""{ "sdk": { "version": "10.0.100" }, "test": { "runner": "Microsoft.Testing.Platform" } }""", true)]
    [InlineData("{\n  // opt in to the new dotnet test experience\n  \"test\": { \"runner\": \"Microsoft.Testing.Platform\" }\n}", true)]
    [InlineData("""{ "test": { "runner": "VSTest" } }""", false)]
    [InlineData("""{ "test": { "runner": "Something.Else" } }""", false)]
    [InlineData("""{ "test": { "runner": 1 } }""", false)]
    [InlineData("""{ "test": { "runner": null } }""", false)]
    [InlineData("""{ "test": { } }""", false)]
    [InlineData("""{ "test": [ "Microsoft.Testing.Platform" ] }""", false)]
    [InlineData("""{ "sdk": { "version": "10.0.100" } }""", false)]
    [InlineData("{}", false)]
    [InlineData("", false)]
    [InlineData("""{ "test": { "runner": "Microsoft.Testing.Platform" """, false)]
    public void ReadsRunnerFromGlobalJson(string globalJson, bool expected)
    {
        File.WriteAllText(Path.Combine(_root, "global.json"), globalJson);

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_root, runnerEnvironmentValue: null).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutStartDirectoryUsesVsTest(string? startDirectory)
    {
        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(startDirectory, runnerEnvironmentValue: null).Should().BeFalse();
    }

    [Fact]
    public void FindsGlobalJsonInParentDirectory()
    {
        File.WriteAllText(Path.Combine(_root, "global.json"), """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");
        var nested = Directory.CreateDirectory(Path.Combine(_root, "src", "Sample.Tests")).FullName;

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(nested, runnerEnvironmentValue: null).Should().BeTrue();
    }

    [Fact]
    public void ClosestGlobalJsonWinsEvenWithoutRunner()
    {
        // The SDK reads the runner from the first global.json it finds and doesn't keep searching upwards.
        File.WriteAllText(Path.Combine(_root, "global.json"), """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");
        var nested = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
        File.WriteAllText(Path.Combine(nested, "global.json"), """{ "sdk": { "version": "10.0.100" } }""");

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(nested, runnerEnvironmentValue: null).Should().BeFalse();
    }

    [Theory]
    [InlineData("Microsoft.Testing.Platform", null, true)]
    [InlineData("microsoft.testing.platform", null, true)]
    [InlineData("  Microsoft.Testing.Platform  ", null, true)]
    [InlineData("Microsoft.Testing.Platform", "Microsoft.Testing.Platform", true)]
    [InlineData("Microsoft.Testing.Platform", "VSTest", true)]
    [InlineData("VSTest", "Microsoft.Testing.Platform", false)]
    [InlineData("vstest", "Microsoft.Testing.Platform", false)]
    [InlineData("VSTest", null, false)]
    [InlineData("unknown", "Microsoft.Testing.Platform", true)]
    [InlineData("unknown", null, false)]
    [InlineData("", "Microsoft.Testing.Platform", true)]
    [InlineData("  ", "Microsoft.Testing.Platform", true)]
    [InlineData(null, null, false)]
    public void EnvironmentVariableOverridesGlobalJsonWhenRecognized(string? environmentValue, string? globalJsonRunner, bool expected)
    {
        // The SDK ignores unrecognized DOTNET_TEST_RUNNER values and falls back to global.json.
        // A global.json without a runner keeps the search from reaching one outside of the test folder.
        var globalJson = globalJsonRunner is null ? "{}" : $$"""{ "test": { "runner": "{{globalJsonRunner}}" } }""";
        File.WriteAllText(Path.Combine(_root, "global.json"), globalJson);

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_root, environmentValue).Should().Be(expected);
    }
}
