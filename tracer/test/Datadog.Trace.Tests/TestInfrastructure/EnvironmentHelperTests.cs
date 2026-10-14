// <copyright file="EnvironmentHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class EnvironmentHelperTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vstest-path-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [CombinatorialData]
    public void FindsInstalledRunner(
        [CombinatorialValues("18", "2022", "2019", "2017")] string version,
        [CombinatorialValues("Enterprise", "Professional", "Community", "BuildTools")] string edition,
        bool x86)
    {
        var root = Path.Combine(_directory, x86 ? "x86" : "x64");
        var path = InstallRunner(root, version, edition);

        EnvironmentHelper.GetVsTestConsolePath(Path.Combine(_directory, "x64"), Path.Combine(_directory, "x86")).Should().Be(path);
    }

    [Fact]
    public void PrefersNewestInstalledRunner()
    {
        InstallRunner(Path.Combine(_directory, "x86"), "2022", "Enterprise");
        var latest = InstallRunner(Path.Combine(_directory, "x64"), "18", "BuildTools");

        EnvironmentHelper.GetVsTestConsolePath(Path.Combine(_directory, "x64"), Path.Combine(_directory, "x86")).Should().Be(latest);
    }

    [Fact]
    public void ReturnsNullWhenNoRunnerIsInstalled()
        => EnvironmentHelper.GetVsTestConsolePath(Path.Combine(_directory, "x64"), Path.Combine(_directory, "x86")).Should().BeNull();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static string InstallRunner(string root, string version, string edition)
    {
        var path = Path.Combine(root, "Microsoft Visual Studio", version, edition, "Common7", "IDE", "CommonExtensions", "Microsoft", "TestWindow", "vstest.console.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}
