// <copyright file="LineProbeResolverTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using FluentAssertions;
using Samples.Probes.TestRuns.SmokeTests;
using Xunit;

namespace Datadog.Trace.Debugger.IntegrationTests;

public class LineProbeResolverTest
{
    private readonly LineProbeResolver _lineProbeResolver;
    private readonly ProbeDefinition _probeDefinition;

    public LineProbeResolverTest()
    {
        var guidGenerator = new DeterministicGuidGenerator();
        _lineProbeResolver = LineProbeResolver.Create(ImmutableHashSet<string>.Empty, ImmutableHashSet<string>.Empty);
        var probeDefinitions = DebuggerTestHelper.GetAllProbes(typeof(LambdaSingleLine), "net461", unlisted: true, guidGenerator);
        _probeDefinition = probeDefinitions.First().Probe;
    }

    [Fact]
    public void ReturnsCorrectMetadata()
    {
        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);
        result.Status.Should().Be(LiveProbeResolveStatus.Bound);
        var module = typeof(LambdaSingleLine).Assembly.ManifestModule;
        var method = module.ResolveMethod(loc.MethodToken);
        loc.MVID.Should().Be(module.ModuleVersionId);
        method.Name.Should().Be("Run");
    }

    [Fact]
    public void TestForwardSlashAndBackslashAreInterchangeable()
    {
        var result1 = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc1);

        // Flip the slashes
        _probeDefinition.Where.SourceFile = _probeDefinition.Where.SourceFile.Contains(@"/") ? _probeDefinition.Where.SourceFile.Replace(@"/", @"\") : _probeDefinition.Where.SourceFile.Replace(@"\", @"/");

        var result2 = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc2);

        result1.Status.Should().Be(LiveProbeResolveStatus.Bound);
        result2.Status.Should().Be(LiveProbeResolveStatus.Bound);
        (loc1.MethodToken, loc1.BytecodeOffset).Should().Be((loc2.MethodToken, loc2.BytecodeOffset));
    }

    [Theory]
    [InlineData(@"D:\build_agent\yada\yada\src\MyProject\MyFile.cs")]
    [InlineData(@"/usr/opt/build_agent/yada/yada/src/MyProject/MyFile.cs")]
    public void TestSlashDirectionIsPreserved(string originalPath)
    {
        // The FilePathLookup is used look up file paths that were originally extracted from the PDB, and we
        // use the result of the lookup to then query the PDB and find the bytecode offset at a given line number.
        // Therefore, the result should preserve the exact original file path that was in the PDB, regardless of
        // whether the search string is using backslash or forward-slash.

        var lookup = new LineProbeResolver.FilePathLookup();

        lookup.InsertPath(originalPath);

        Assert.Equal(originalPath, lookup.FindPathThatEndsWith(@"src\MyProject\MyFile.cs"));
        Assert.Equal(originalPath, lookup.FindPathThatEndsWith(@"src/MyProject/MyFile.cs"));
        Assert.Equal(originalPath, lookup.FindPathThatEndsWith(@"src/MyProject\MyFile.cs"));
    }

    [Fact]
    public void OutOfBoundsLineProbeReturnsAnError()
    {
        _probeDefinition.Where.Lines[0] = "999999";
        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Error);
        loc.Should().BeNull();
    }

    [Fact]
    public void ResolveLineThatIncludesLambdaExpressions()
    {
        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Bound);
        var method = typeof(LambdaSingleLine).Assembly.ManifestModule.ResolveMethod(loc.MethodToken);
        method.Name.Should().Be("Run");
    }
}
