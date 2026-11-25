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
        _root = Path.Combine(Path.GetTempPath(), "dd-mtp-detector-" + Path.GetRandomFileName());
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

        Detect(_workingDirectory, testRunnerEnvironmentValue: null).Should().Be(expected);
    }

    [Fact]
    public void FindsGlobalJsonInAParentDirectory()
    {
        File.WriteAllText(Path.Combine(_root, "repo", "global.json"), MtpGlobalJson);

        Detect(_workingDirectory, testRunnerEnvironmentValue: null).Should().BeTrue();
    }

    [Fact]
    public void OnlyTheClosestGlobalJsonCounts()
    {
        File.WriteAllText(Path.Combine(_root, "repo", "global.json"), MtpGlobalJson);
        File.WriteAllText(Path.Combine(_workingDirectory, "global.json"), """{ "sdk": { "version": "10.0.100" } }""");

        Detect(_workingDirectory, testRunnerEnvironmentValue: null).Should().BeFalse();
    }

    [Fact]
    public void NoGlobalJsonMeansVsTest()
    {
        Detect(_workingDirectory, testRunnerEnvironmentValue: null).Should().BeFalse();
        Detect(workingDirectory: null, testRunnerEnvironmentValue: null).Should().BeFalse();
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

        Detect(_workingDirectory, environmentValue).Should().Be(expected);
    }

    [Theory]
    [InlineData(MtpGlobalJson, null, 8, false)]
    [InlineData(MtpGlobalJson, null, 9, false)]
    [InlineData(MtpGlobalJson, null, 10, true)]
    [InlineData(MtpGlobalJson, null, 11, true)]
    [InlineData(MtpGlobalJson, null, null, false)]
    [InlineData(null, "Microsoft.Testing.Platform", 9, false)]
    [InlineData(null, "Microsoft.Testing.Platform", 10, false)]
    [InlineData(null, "Microsoft.Testing.Platform", 11, true)]
    [InlineData(MtpGlobalJson, "VSTest", 10, true)]
    [InlineData(MtpGlobalJson, "VSTest", 11, false)]
    [InlineData(VsTestGlobalJson, "Microsoft.Testing.Platform", 10, false)]
    [InlineData(VsTestGlobalJson, "Microsoft.Testing.Platform", 11, true)]
    public void OnlyTheSdkVersionsThatReadTheSettingSelectTestingPlatform(string globalJson, string environmentValue, int? sdkMajorVersion, bool expected)
    {
        if (globalJson is not null)
        {
            File.WriteAllText(Path.Combine(_workingDirectory, "global.json"), globalJson);
        }

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_workingDirectory, environmentValue, () => sdkMajorVersion).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(VsTestGlobalJson, null)]
    [InlineData(VsTestGlobalJson, "VSTest")]
    [InlineData(null, "Bogus")]
    public void DoesNotReadTheSdkVersionWhenNothingSelectsTestingPlatform(string globalJson, string environmentValue)
    {
        if (globalJson is not null)
        {
            File.WriteAllText(Path.Combine(_workingDirectory, "global.json"), globalJson);
        }

        var sdkVersionRead = false;
        int? ReadSdkVersion()
        {
            sdkVersionRead = true;
            return 11;
        }

        TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(_workingDirectory, environmentValue, ReadSdkVersion).Should().BeFalse();

        sdkVersionRead.Should().BeFalse();
    }

    [Fact]
    public void ReadsTheMajorVersionOfTheSdk()
    {
        TestingPlatformRunnerDetector.GetSdkMajorVersion("dotnet", _workingDirectory).Should().BeGreaterOrEqualTo(8);
    }

    [Fact]
    public void AnSdkThatCannotRunHasNoVersion()
    {
        TestingPlatformRunnerDetector.GetSdkMajorVersion(Path.Combine(_root, "missing-dotnet"), _workingDirectory).Should().BeNull();
    }

    private static bool Detect(string workingDirectory, string testRunnerEnvironmentValue)
        => TestingPlatformRunnerDetector.IsDotnetTestUsingTestingPlatform(workingDirectory, testRunnerEnvironmentValue, () => 11);
}
