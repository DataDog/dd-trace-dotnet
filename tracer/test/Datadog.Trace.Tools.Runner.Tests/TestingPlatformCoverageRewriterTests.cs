// <copyright file="TestingPlatformCoverageRewriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Attributes;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using FluentAssertions;
using Mono.Cecil;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class TestingPlatformCoverageRewriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd-mtp-rewriter-" + Path.GetRandomFileName());
    private readonly string _tracerHome;
    private readonly TestOptimizationSettings _settings = new(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance);

    public TestingPlatformCoverageRewriterTests()
    {
        // The rewrite copies Datadog.Trace.dll from the folder that matches the target framework of the assembly, netstandard2.0 for the fixture.
        _tracerHome = Path.Combine(_root, "home");
        var tracerFolder = Directory.CreateDirectory(Path.Combine(_tracerHome, "netstandard2.0")).FullName;
        File.Copy("Datadog.Trace.dll", Path.Combine(tracerFolder, "Datadog.Trace.dll"));
        File.Copy("Datadog.Trace.pdb", Path.Combine(tracerFolder, "Datadog.Trace.pdb"));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void RewritesTheTestAssembliesAndRestoresThemWhenDisposed()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out var reason)
                                       .Should().BeTrue(reason);
        using (rewriter)
        {
            reason.Should().BeNull();
            rewriter!.AssembliesProcessed.Should().Be(1);
            HasCoverage(Path.Combine(output, "Sample.Tests.dll")).Should().BeTrue();
            File.Exists(Path.Combine(output, "Datadog.Trace.dll")).Should().BeTrue();
            File.ReadAllText(Path.Combine(output, "Sample.Tests.deps.json")).Should().Contain("Datadog.Trace/");
        }

        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void RestoresWhenTheRunEndsWithoutTheCallerDisposing()
    {
        // The runner disposes in a finally block, and also when the process exits: disposing twice is harmless.
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);
        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out _).Should().BeTrue();

        rewriter!.Dispose();
        rewriter.Dispose();

        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void LeavesAnAssemblyThatAlreadyHasCoverageAsItFoundIt()
    {
        var output = CreateOutputFolder("net10.0");
        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var first, out _).Should().BeTrue();
        using (first)
        {
            // Simulates a run that was killed before it could restore: the next run starts from the rewritten files.
            var rewrittenHash = Hash(Path.Combine(output, "Sample.Tests.dll"));

            TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var second, out _).Should().BeTrue();
            second!.Dispose();

            Hash(Path.Combine(output, "Sample.Tests.dll")).Should().Be(rewrittenHash);
            HasCoverage(Path.Combine(output, "Sample.Tests.dll")).Should().BeTrue();
        }
    }

    [Fact]
    public void RewritesEveryOutputFolderOfTheMatchingAssemblies()
    {
        var net9 = CreateOutputFolder("net9.0");
        var net10 = CreateOutputFolder("net10.0");

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeTrue(reason);
        using (rewriter)
        {
            rewriter!.AssembliesProcessed.Should().Be(2);
            HasCoverage(Path.Combine(net9, "Sample.Tests.dll")).Should().BeTrue();
            HasCoverage(Path.Combine(net10, "Sample.Tests.dll")).Should().BeTrue();
        }
    }

    [Fact]
    public void DoesNotRewriteWhenTheCommandBuilds()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "Sample.slnx"], _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().Contain("--no-build");
        Snapshot(output).Should().Equal(original);
    }

    [Theory]
    [InlineData("--arch", "x64")]
    [InlineData("--os", "linux")]
    [InlineData("--use-current-runtime", null)]
    public void DoesNotRewriteForOptionsThatChangeTheOutputFolder(string option, string? value)
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);
        string[] arguments = value is null
                                 ? ["test", "--no-build", option]
                                 : ["test", "--no-build", option, value];

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", arguments, _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().Contain(option);
        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void DoesNotRewriteWithoutATracerHome()
    {
        var output = CreateOutputFolder("net10.0");
        var original = Snapshot(output);

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, tracerHome: null, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().NotBeNullOrEmpty();
        Snapshot(output).Should().Equal(original);
    }

    [Fact]
    public void DoesNotRewriteWhenNoAssemblyMatches()
    {
        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "**/Missing.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().Contain("output folders");
    }

    [Fact]
    public void DoesNotRewriteAnAssemblyWithoutSymbols()
    {
        var output = CreateOutputFolder("net10.0");
        File.Delete(Path.Combine(output, "Sample.Tests.pdb"));
        var original = Snapshot(output);

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", _root], _root, _tracerHome, _settings, out var rewriter, out var reason).Should().BeFalse();

        rewriter.Should().BeNull();
        reason.Should().Contain("symbols");
        Snapshot(output).Should().Equal(original);
    }

    private static bool HasCoverage(string assemblyPath)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { InMemory = true });
        return assembly.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == typeof(CoveredAssemblyAttribute).FullName);
    }

    private static string Hash(string path)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }

    private string[] Snapshot(string folder)
        => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => $"{path.Substring(folder.Length)}:{Hash(path)}")
                    .ToArray();

    private string CreateOutputFolder(string framework)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "project", "bin", "Release", framework)).FullName;
        File.Copy("CoverageRewriterAssembly.dll", Path.Combine(folder, "Sample.Tests.dll"));
        File.Copy("CoverageRewriterAssembly.pdb", Path.Combine(folder, "Sample.Tests.pdb"));
        File.WriteAllText(
            Path.Combine(folder, "Sample.Tests.deps.json"),
            """{ "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0" }, "targets": { ".NETCoreApp,Version=v10.0": { "Sample.Tests/1.0.0": { "runtime": { "Sample.Tests.dll": { } } } } }, "libraries": { "Sample.Tests/1.0.0": { "type": "project", "serviceable": false, "sha512": "" } } }""");
        return folder;
    }
}
