// <copyright file="LineProbeResolverTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.Debugger.Models;
using FluentAssertions;
using Samples.Probes.TestRuns.SmokeTests;
using Xunit;

#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Immutable;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
#endif

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
        loc.Mvid.Should().Be(module.ModuleVersionId);
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
    [InlineData(@"\\build-agent\share\yada\yada\src\MyProject\MyFile.cs")]
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

    [Theory]
    [InlineData(@"D:\build_agent\yada\yada\src\MyProject\MyFile.cs", @"src\MyProject\MyFile.cs\")]
    [InlineData(@"/usr/opt/build_agent/yada/yada/src/MyProject/MyFile.cs", @"src/MyProject/MyFile.cs/")]
    [InlineData(@"\\build-agent\share\yada\yada\src\MyProject\MyFile.cs", @"src\MyProject\MyFile.cs\")]
    public void FindPathThatEndsWithIgnoresTrailingSeparatorsInQuery(string originalPath, string queryPath)
    {
        var lookup = new LineProbeResolver.FilePathLookup();

        lookup.InsertPath(originalPath);

        lookup.FindPathThatEndsWith(queryPath).Should().Be(originalPath);
    }

    [Fact]
    public void FallbackMatchBindsWhenOnlyLeadingSegmentsDiffer()
    {
        _probeDefinition.Where.SourceFile = @"Source\" + _probeDefinition.Where.SourceFile.Replace('/', '\\');

        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Bound);
        result.Diagnostics.PathMatchType.Should().Be(LineProbePathMatchType.FallbackTrailingSuffixMatch);
        result.Diagnostics.MatchingTrailingSegments.Should().BeGreaterThanOrEqualTo(4);
        loc.Should().NotBeNull();
    }

    [Fact]
    public void FilePathLookupFindsClosestUniqueSuffixMatchForLinuxPaths()
    {
        var lookup = new LineProbeResolver.FilePathLookup();
        const string documentPath = @"/_/src/MyProject/Feature/MyFile.cs";

        lookup.InsertPath(documentPath);

        lookup.TryFindClosestPathBySuffix(@"Source/src/MyProject/Feature/MyFile.cs", minimumMatchingTrailingSegments: 4, out var match, out var matchingTrailingSegments).Should().BeTrue();
        match.Should().Be(documentPath);
        matchingTrailingSegments.Should().Be(4);
    }

    [Fact]
    public void FilePathLookupFindsClosestUniqueSuffixMatchForWindowsPaths()
    {
        var lookup = new LineProbeResolver.FilePathLookup();
        const string documentPath = @"D:\agent\src\MyProject\Feature\MyFile.cs";

        lookup.InsertPath(documentPath);

        lookup.TryFindClosestPathBySuffix(@"Source\src\MyProject\Feature\MyFile.cs", minimumMatchingTrailingSegments: 4, out var match, out var matchingTrailingSegments).Should().BeTrue();
        match.Should().Be(documentPath);
        matchingTrailingSegments.Should().Be(4);
    }

    [Theory]
    [InlineData(@"/_/src/MyProject/Feature/MyFile.cs", @"Source/src/MyProject/Feature/MyFile.cs/")]
    [InlineData(@"D:\agent\src\MyProject\Feature\MyFile.cs", @"Source\src\MyProject\Feature\MyFile.cs\")]
    public void FilePathLookupFallbackIgnoresTrailingSeparators(string documentPath, string queryPath)
    {
        var lookup = new LineProbeResolver.FilePathLookup();

        lookup.InsertPath(documentPath);

        lookup.TryFindClosestPathBySuffix(queryPath, minimumMatchingTrailingSegments: 4, out var match, out var matchingTrailingSegments).Should().BeTrue();
        match.Should().Be(documentPath);
        matchingTrailingSegments.Should().Be(4);
    }

    [Fact]
    public void FilePathLookupDoesNotFallbackOnCaseOnlyDifference()
    {
        var lookup = new LineProbeResolver.FilePathLookup();
        const string documentPath = @"/_/src/MyProject/Feature/MyFile.cs";

        lookup.InsertPath(documentPath);

        lookup.TryFindClosestPathBySuffix(@"Source/src/MyProject/Feature/myfile.cs", minimumMatchingTrailingSegments: 4, out var match, out var matchingTrailingSegments).Should().BeFalse();
        match.Should().BeNull();
        matchingTrailingSegments.Should().Be(0);
    }

    [Fact]
    public void FilePathLookupDoesNotFallbackOnFileNameOnlyMatch()
    {
        var lookup = new LineProbeResolver.FilePathLookup();

        lookup.InsertPath(@"/_/src/Other/MyFile.cs");

        lookup.TryFindClosestPathBySuffix(@"Source/src/MyProject/Feature/MyFile.cs", minimumMatchingTrailingSegments: 4, out var match, out var matchingTrailingSegments).Should().BeFalse();
        match.Should().BeNull();
        matchingTrailingSegments.Should().Be(0);
    }

    [Fact]
    public void FilePathLookupPrefersLongestUniqueSuffixMatch()
    {
        var lookup = new LineProbeResolver.FilePathLookup();
        const string bestPath = @"/_/src/One/Feature/MyFile.cs";

        lookup.InsertPath(bestPath);
        lookup.InsertPath(@"/_/src/Feature/MyFile.cs");

        lookup.TryFindClosestPathBySuffix(@"Source/src/One/Feature/MyFile.cs", minimumMatchingTrailingSegments: 2, out var match, out var matchingTrailingSegments).Should().BeTrue();
        match.Should().Be(bestPath);
        matchingTrailingSegments.Should().Be(4);
    }

    [Fact]
    public void FilePathLookupRejectsAmbiguousSuffixMatches()
    {
        var lookup = new LineProbeResolver.FilePathLookup();

        lookup.InsertPath(@"/a/src/Shared/Feature/MyFile.cs");
        lookup.InsertPath(@"/b/src/Shared/Feature/MyFile.cs");

        lookup.TryFindClosestPathBySuffix(@"Source/src/Shared/Feature/MyFile.cs", minimumMatchingTrailingSegments: 4, out var match, out var matchingTrailingSegments).Should().BeFalse();
        match.Should().BeNull();
        matchingTrailingSegments.Should().Be(0);
    }

    [Fact]
    public void BestFallbackMatchSelectionRejectsTieWhenHighestScoreIsAlreadyAmbiguous()
    {
        var selection = new LineProbeResolver.BestFallbackMatchSelection();

        selection.Track(typeof(string).Assembly, CreateClosestPathBySuffixResult(@"/a/src/Shared/Feature/MyFile.cs", matchingTrailingSegments: 4, isAmbiguous: true));
        selection.Track(typeof(LineProbeResolverTest).Assembly, CreateClosestPathBySuffixResult(@"/b/src/Shared/Feature/MyFile.cs", matchingTrailingSegments: 4, isAmbiguous: false));

        selection.HasAmbiguousBestMatch.Should().BeTrue();
        selection.BestMatch.Should().BeNull();
    }

    [Fact]
    public void BestFallbackMatchSelectionAllowsHigherUniqueScoreToOverrideEarlierAmbiguity()
    {
        var selection = new LineProbeResolver.BestFallbackMatchSelection();

        selection.Track(typeof(string).Assembly, CreateClosestPathBySuffixResult(@"/a/src/Shared/Feature/MyFile.cs", matchingTrailingSegments: 4, isAmbiguous: true));
        selection.Track(typeof(LineProbeResolverTest).Assembly, CreateClosestPathBySuffixResult(@"/b/src/One/Shared/Feature/MyFile.cs", matchingTrailingSegments: 5, isAmbiguous: false));

        selection.HasAmbiguousBestMatch.Should().BeFalse();
        selection.BestMatch.Should().NotBeNull();
        selection.BestMatch!.Value.Path.Should().Be(@"/b/src/One/Shared/Feature/MyFile.cs");
        selection.BestMatch!.Value.MatchingTrailingSegments.Should().Be(5);
    }

    [Fact]
    public void OutOfBoundsLineProbeReturnsAnError()
    {
        _probeDefinition.Where.Lines[0] = "999999";
        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Error);
        result.Reason.Should().Be(LineProbeResolveReason.MissingSequencePoint);
        result.Diagnostics.ProbeLine.Should().Be(999999);
        loc.Should().BeNull();
    }

    [Fact]
    public void InvalidLineNumberReturnsRawLinesDiagnostics()
    {
        _probeDefinition.Where.Lines[0] = "not-a-number";

        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Error);
        result.Reason.Should().Be(LineProbeResolveReason.InvalidLineNumber);
        result.Diagnostics.RawLines.Should().Be("not-a-number");
        result.Diagnostics.ProbeLine.Should().BeNull();
        loc.Should().BeNull();
    }

    [Fact]
    public void SameFileNameMatchAddsPathHintWithoutChangingUnboundReason()
    {
        _probeDefinition.Where.SourceFile = @"some\other\folder\LambdaSingleLine.cs";

        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Unbound);
        result.Reason.Should().Be(LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable);
        result.Diagnostics.LoadedAssemblyCount.Should().BeGreaterThan(0);
        result.Diagnostics.SymbolicatedAssemblyCount.Should().BeGreaterThan(0);
        result.Diagnostics.SameFileNameMatchCount.Should().BeGreaterThan(0);
        result.Diagnostics.SameFileNameExamples.Should().NotBeNullOrEmpty();
		result.Diagnostics.FallbackFailureReason.Should().Be(LineProbeFallbackFailureReason.NoQualifiedSuffixMatch);
        result.Diagnostics.MatchingTrailingSegments.Should().Be(1);
        result.Diagnostics.QualifiedFallbackMatchCount.Should().Be(0);
        result.Message.Should().Contain("did not match the PDB document path");
        result.Message.Should().Contain("assembly is not loaded yet");
        result.Message.Should().Contain("symbols are unavailable");
        result.Message.Should().Contain("configured source path may differ from the PDB document path");
        loc.Should().BeNull();
    }

    [Fact]
    public void UnknownFileReturnsUnboundWithGenericMessage()
    {
        _probeDefinition.Where.SourceFile = @"some\other\folder\FileThatDoesNotExistAnywhere.cs";

        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Unbound);
        result.Reason.Should().Be(LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable);
        result.Diagnostics.SameFileNameMatchCount.Should().Be(0);
        result.Message.Should().Contain("assembly is not loaded yet");
        result.Message.Should().Contain("symbols are unavailable");
        result.Message.Should().NotContain("configured source path may differ from the PDB document path");
        loc.Should().BeNull();
    }

    [Fact]
    public void UnknownFileReturnsUnboundWithGenericReason()
    {
        _probeDefinition.Where.SourceFile = @"some\other\folder\FileThatDoesNotExistAnywhere.cs";

        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Unbound);
        result.Reason.Should().Be(LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable);
        result.Diagnostics.FallbackFailureReason.Should().Be(LineProbeFallbackFailureReason.NoSameFileNameCandidates);
        result.Diagnostics.SameFileNameMatchCount.Should().Be(0);
        result.Diagnostics.QualifiedFallbackMatchCount.Should().Be(0);
        result.Message.Should().Contain("assembly is not loaded yet or if symbols are unavailable");
        loc.Should().BeNull();
    }

    [Fact]
    public void ResolveLineThatIncludesLambdaExpressions()
    {
        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc);

        result.Status.Should().Be(LiveProbeResolveStatus.Bound);
        result.Diagnostics.PathMatchType.Should().Be(LineProbePathMatchType.ExactSuffixMatch);
        result.Diagnostics.MatchingTrailingSegments.Should().BeNull();
        var method = typeof(LambdaSingleLine).Assembly.ManifestModule.ResolveMethod(loc.MethodToken);
        method.Name.Should().Be("Run");
    }

    [Fact]
    public void MinimalDiagnosticsOnBoundResolutionOmitDetailedFields()
    {
        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc, LineProbeDiagnosticLevel.Minimal);

        result.Status.Should().Be(LiveProbeResolveStatus.Bound);
        result.Diagnostics.ProbeFile.Should().Be(_probeDefinition.Where.SourceFile);
        result.Diagnostics.ProbeLine.Should().Be(int.Parse(_probeDefinition.Where.Lines[0]));
        result.Diagnostics.PathMatchType.Should().BeNull();
        result.Diagnostics.ResolvedSourceFile.Should().BeNull();
        result.Diagnostics.AssemblyName.Should().BeNull();
        result.Diagnostics.SameFileNameExamples.Should().BeNull();
        loc.Should().NotBeNull();
    }

    [Fact]
    public void MinimalDiagnosticsOnUnboundResolutionKeepReasonButSkipDetailedFields()
    {
        _probeDefinition.Where.SourceFile = @"some\other\folder\LambdaSingleLine.cs";

        var result = _lineProbeResolver.TryResolveLineProbe(_probeDefinition, out var loc, LineProbeDiagnosticLevel.Minimal);

        result.Status.Should().Be(LiveProbeResolveStatus.Unbound);
        result.Reason.Should().Be(LineProbeResolveReason.LoadedAssemblySourceFileMismatch);
        result.Diagnostics.ProbeFile.Should().Be(_probeDefinition.Where.SourceFile);
        result.Diagnostics.ProbeLine.Should().Be(int.Parse(_probeDefinition.Where.Lines[0]));
        result.Diagnostics.MatchingTrailingSegments.Should().BeNull();
        result.Diagnostics.FallbackFailureReason.Should().BeNull();
        result.Diagnostics.SameFileNameExamples.Should().BeNull();
        loc.Should().BeNull();
    }

    private static LineProbeResolver.ClosestPathBySuffixResult CreateClosestPathBySuffixResult(string path, int matchingTrailingSegments, bool isAmbiguous)
    {
        return new LineProbeResolver.ClosestPathBySuffixResult(
            exampleSameFileNamePath: path,
            bestMatchingTrailingSegments: matchingTrailingSegments,
            qualifiedMatchCount: isAmbiguous ? 2 : 1,
            bestQualifiedMatchingTrailingSegments: matchingTrailingSegments,
            bestQualifiedPath: path,
            hasAmbiguousBestQualifiedMatch: isAmbiguous);
    }
}
