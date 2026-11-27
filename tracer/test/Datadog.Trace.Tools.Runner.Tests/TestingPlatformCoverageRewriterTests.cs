// <copyright file="TestingPlatformCoverageRewriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Mono.Cecil;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public sealed class TestingPlatformCoverageRewriterTests : IDisposable
{
    private const string CoveredAssemblyAttributeName = "Datadog.Trace.Ci.Coverage.Attributes.CoveredAssemblyAttribute";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dd-mtp-rewriter-" + Path.GetRandomFileName());
    private readonly string _output;
    private readonly string _tracerHome;
    private readonly TestOptimizationSettings _settings = new(new DictionaryConfigurationSource(new Dictionary<string, string>()), NullConfigurationTelemetry.Instance);

    public TestingPlatformCoverageRewriterTests()
    {
        _output = Directory.CreateDirectory(Path.Combine(_root, "Tests", "bin", "Release", "net10.0")).FullName;
        _tracerHome = Directory.CreateDirectory(Path.Combine(_root, "home")).FullName;
        var tracerAssembly = typeof(Tracer).Assembly.Location;
        foreach (var folder in new[] { "net461", "netstandard2.0", "netcoreapp3.1", "net6.0" })
        {
            var target = Directory.CreateDirectory(Path.Combine(_tracerHome, folder)).FullName;
            File.Copy(tracerAssembly, Path.Combine(target, Path.GetFileName(tracerAssembly)));
        }
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
    [InlineData("test", "--test-modules", "Tests/**/Sample.Tests.dll")]
    [InlineData("test", "--test-modules", "**/Sample.Tests.dll", "--root-directory", "Tests")]
    public void RewritesTheTestModules(params string[] arguments)
    {
        var assemblyPath = CompileTestAssembly();

        var rewritten = TestingPlatformCoverageRewriter.TryRewrite("dotnet", arguments, _root, _tracerHome, _settings, out var reason);

        rewritten.Should().BeTrue(reason);
        IsRewritten(assemblyPath).Should().BeTrue();
        File.Exists(Path.Combine(_output, "Datadog.Trace.dll")).Should().BeTrue();
    }

    [Fact]
    public void LeavesAnAlreadyRewrittenAssemblyAsItIs()
    {
        var assemblyPath = CompileTestAssembly();
        string[] arguments = ["test", "--test-modules", "Tests/**/Sample.Tests.dll"];
        TestingPlatformCoverageRewriter.TryRewrite("dotnet", arguments, _root, _tracerHome, _settings, out _).Should().BeTrue();
        var firstRewrite = File.ReadAllBytes(assemblyPath);

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", arguments, _root, _tracerHome, _settings, out var reason).Should().BeTrue(reason);

        File.ReadAllBytes(assemblyPath).Should().Equal(firstRewrite);
    }

    [Theory]
    [InlineData("--no-build", new[] { "test", "Tests/Tests.csproj" })]
    [InlineData("--no-build", new[] { "test", "--project", "Tests", "-c", "Release" })]
    [InlineData("'-a'", new[] { "test", "--no-build", "-a", "x64", "--project", "Tests" })]
    [InlineData("output folders", new[] { "test", "--test-modules", "Tests/**/Missing.dll" })]
    public void DoesNotRewriteWhenTheAssembliesCannotBeFound(string expectedReason, string[] arguments)
    {
        var assemblyPath = CompileTestAssembly();
        var original = File.ReadAllBytes(assemblyPath);

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", arguments, _root, _tracerHome, _settings, out var reason).Should().BeFalse();

        reason.Should().Contain(expectedReason);

        File.ReadAllBytes(assemblyPath).Should().Equal(original);
    }

    [Fact]
    public void DoesNotFailWhenTheTestProjectWasNotBuilt()
    {
        WriteTestingPlatformProject("Unbuilt", "net8.0");

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--no-build", "--project", "Unbuilt"], _root, _tracerHome, _settings, out var reason).Should().BeFalse();

        reason.Should().Contain("don't exist");
    }

    [Fact]
    public void RewritesTheTargetFrameworksThatWereBuilt()
    {
        WriteTestingPlatformProject("Tests", "net8.0;net10.0");
        var assemblyPath = CompileTestAssembly();

        // Only net10.0 was built (with -c Release), net8.0 has no output folder.
        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--no-build", "--project", "Tests", "-c", "Release"], _root, _tracerHome, _settings, out var reason).Should().BeTrue(reason);

        IsRewritten(assemblyPath).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    public void DoesNotRewriteWithoutTracerHome(string tracerHome)
    {
        var assemblyPath = CompileTestAssembly();
        var original = File.ReadAllBytes(assemblyPath);

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "Tests/**/Sample.Tests.dll"], _root, tracerHome is null ? null : Path.Combine(_root, tracerHome), _settings, out var reason).Should().BeFalse();

        reason.Should().Contain("tracer home");
        File.ReadAllBytes(assemblyPath).Should().Equal(original);
    }

    [Fact]
    public void DoesNotRewriteAssembliesWithoutSymbols()
    {
        var assemblyPath = CompileTestAssembly();
        File.Delete(Path.ChangeExtension(assemblyPath, ".pdb"));
        var original = File.ReadAllBytes(assemblyPath);

        TestingPlatformCoverageRewriter.TryRewrite("dotnet", ["test", "--test-modules", "Tests/**/Sample.Tests.dll"], _root, _tracerHome, _settings, out var reason).Should().BeFalse();

        reason.Should().Contain(".pdb");
        File.ReadAllBytes(assemblyPath).Should().Equal(original);
    }

    private static bool IsRewritten(string assemblyPath)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { InMemory = true });
        return assembly.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == CoveredAssemblyAttributeName);
    }

    private void WriteTestingPlatformProject(string name, string targetFrameworks)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        var project = $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFrameworks>{targetFrameworks}</TargetFrameworks>
                 <OutputType>Exe</OutputType>
                 <AssemblyName>Sample.Tests</AssemblyName>
                 <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
               </PropertyGroup>
             </Project>
             """;
        File.WriteAllText(Path.Combine(folder, name + ".csproj"), project);
    }

    private string CompileTestAssembly()
    {
        var assemblyPath = Path.Combine(_output, "Sample.Tests.dll");
        TestAssemblyCompiler.CompileCSharp(
            assemblyPath,
            optimize: false,
            ("Calculator.cs", "public static class Calculator { public static int Add(int a, int b) { return a + b; } }"));
        return assemblyPath;
    }
}
