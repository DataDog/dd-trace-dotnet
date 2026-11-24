// <copyright file="TestingPlatformRunnerDetectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public sealed class TestingPlatformRunnerDetectorTests : IDisposable
{
    private const string MtpGlobalJson = """{ "test": { "runner": "Microsoft.Testing.Platform" } }""";
    private const string VsTestGlobalJson = """{ "test": { "runner": "VSTest" } }""";

    private readonly string _root;
    private readonly string _workingDirectory;

    public TestingPlatformRunnerDetectorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dd-mtp-detector-" + Guid.NewGuid().ToString("N"));
        _workingDirectory = Path.Combine(_root, "repo", "tests");
        Directory.CreateDirectory(_workingDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    [Theory]
    [InlineData(MtpGlobalJson, true)]
    [InlineData("""{ "test": { "runner": "microsoft.testing.platform" } }""", true)]
    [InlineData("""{ "sdk": { "version": "10.0.100" }, "test": { "runner": "Microsoft.Testing.Platform" } }""", true)]
    [InlineData("{\n  // MTP\n  \"test\": { \"runner\": \"Microsoft.Testing.Platform\" }\n}", true)]
    [InlineData(VsTestGlobalJson, false)]
    [InlineData("""{ "sdk": { "version": "10.0.100" } }""", false)]
    [InlineData("""{ "test": { } }""", false)]
    [InlineData("""{ "test": { "runner": 5 } }""", false)]
    [InlineData("""{ "test": "Microsoft.Testing.Platform" }""", false)]
    [InlineData("""{ "test": { "runner": "Microsoft.Testing.Platform" """, false)]
    [InlineData("", false)]
    public void ReadsTheRunnerFromGlobalJson(string globalJson, bool expected)
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "global.json"), globalJson);

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_workingDirectory, testRunnerEnvironmentValue: null).Should().Be(expected);
    }

    [Fact]
    public void FindsGlobalJsonInAParentDirectory()
    {
        File.WriteAllText(Path.Combine(_root, "repo", "global.json"), MtpGlobalJson);

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_workingDirectory, testRunnerEnvironmentValue: null).Should().BeTrue();
    }

    [Fact]
    public void OnlyTheClosestGlobalJsonCounts()
    {
        File.WriteAllText(Path.Combine(_root, "repo", "global.json"), MtpGlobalJson);
        File.WriteAllText(Path.Combine(_workingDirectory, "global.json"), """{ "sdk": { "version": "10.0.100" } }""");

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_workingDirectory, testRunnerEnvironmentValue: null).Should().BeFalse();
    }

    [Fact]
    public void NoGlobalJsonMeansVsTest()
    {
        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_workingDirectory, testRunnerEnvironmentValue: null).Should().BeFalse();
        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(workingDirectory: null, testRunnerEnvironmentValue: null).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "Microsoft.Testing.Platform", true)]
    [InlineData(null, "microsoft.testing.platform", true)]
    [InlineData(null, " Microsoft.Testing.Platform ", true)]
    [InlineData(VsTestGlobalJson, "Microsoft.Testing.Platform", true)]
    [InlineData(MtpGlobalJson, "VSTest", false)]
    [InlineData(MtpGlobalJson, "vstest", false)]
    [InlineData(MtpGlobalJson, "Bogus", true)]
    [InlineData(MtpGlobalJson, "", true)]
    [InlineData(VsTestGlobalJson, "Bogus", false)]
    public void RecognizedEnvironmentValueWinsOverGlobalJson(string globalJson, string environmentValue, bool expected)
    {
        if (globalJson is not null)
        {
            File.WriteAllText(Path.Combine(_workingDirectory, "global.json"), globalJson);
        }

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_workingDirectory, environmentValue).Should().Be(expected);
    }
}
