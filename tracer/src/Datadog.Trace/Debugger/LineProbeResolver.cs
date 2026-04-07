// <copyright file="LineProbeResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;

namespace Datadog.Trace.Debugger
{
    internal sealed class LineProbeResolver : ILineProbeResolver
    {
        private const int MinTrailingSegmentsForFallbackMatch = 4;
        private const int MaxSameFileNameExamples = 3;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LineProbeResolver>();
        private readonly ImmutableHashSet<string> _thirdPartyDetectionExcludes;
        private readonly ImmutableHashSet<string> _thirdPartyDetectionIncludes;

        private readonly object _locker;
        private readonly Dictionary<Assembly, FilePathLookup?> _loadedAssemblies;

        private LineProbeResolver(ImmutableHashSet<string> thirdPartyDetectionExcludes, ImmutableHashSet<string> thirdPartyDetectionIncludes)
        {
            _locker = new object();
            _loadedAssemblies = new Dictionary<Assembly, FilePathLookup?>();
            _thirdPartyDetectionExcludes = thirdPartyDetectionExcludes;
            _thirdPartyDetectionIncludes = thirdPartyDetectionIncludes;
        }

        public static LineProbeResolver Create(ImmutableHashSet<string> thirdPartyDetectionExcludes, ImmutableHashSet<string> thirdPartyDetectionIncludes)
        {
            return new LineProbeResolver(thirdPartyDetectionExcludes, thirdPartyDetectionIncludes);
        }

        private static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var span = path.AsSpan();
            TrimTrailingSeparators(ref span);
            if (span.IsEmpty)
            {
                return string.Empty;
            }

            var separatorIndex = FindLastSeparatorIndex(span);
            return separatorIndex >= 0 ? span.Slice(separatorIndex + 1).ToString() : span.ToString();
        }

        private static string BuildLoadedAssemblySourceFileMismatchMessage()
        {
            return "Source file location for probe did not match the PDB document path of a loaded, symbolicated assembly with the same file name.";
        }

        private static string BuildAssemblyNotLoadedOrSymbolsUnavailableMessage()
        {
            return "Source file location for probe was not found in any currently loaded assembly. This can happen if the relevant assembly is not loaded yet or if symbols are unavailable for the matching assembly.";
        }

        private static bool IsDirectorySeparator(char value) => value is '\\' or '/';

        private static int FindLastSeparatorIndex(ReadOnlySpan<char> path)
        {
            for (var i = path.Length - 1; i >= 0; i--)
            {
                if (IsDirectorySeparator(path[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void TrimTrailingSeparators(ref ReadOnlySpan<char> path)
        {
            while (!path.IsEmpty && IsDirectorySeparator(path[path.Length - 1]))
            {
                path = path.Slice(0, path.Length - 1);
            }
        }

        private static void TrackClosestPathMatch(
            Assembly assembly,
            ClosestPathBySuffixResult result,
            bool includeExamplePaths,
            ref int sameFileNameMatchCount,
            ref int bestMatchingTrailingSegments,
            ref int qualifiedFallbackMatchCount,
            ref List<string>? sameFileNameMatches)
        {
            if (result.ExampleSameFileNamePath is null)
            {
                return;
            }

            sameFileNameMatchCount++;
            if (includeExamplePaths && (sameFileNameMatches?.Count ?? 0) < MaxSameFileNameExamples)
            {
                sameFileNameMatches ??= [];
                sameFileNameMatches.Add($"{assembly.GetName().Name}:{result.ExampleSameFileNamePath}");
            }

            if (result.BestMatchingTrailingSegments > bestMatchingTrailingSegments)
            {
                bestMatchingTrailingSegments = result.BestMatchingTrailingSegments;
            }

            qualifiedFallbackMatchCount += result.QualifiedMatchCount;
        }

        private static LineProbeResolutionDiagnostics BuildMinimalDiagnostics(string probeFile, int? probeLine, string probeId)
        {
            return new LineProbeResolutionDiagnostics(ProbeFile: probeFile, ProbeLine: probeLine, ProbeId: probeId);
        }

        private static LineProbeResolutionDiagnostics BuildResolvedAssemblyDiagnostics(string sourceFile, int lineNum, AssemblyPathMatch assemblyPathMatch, string probeId, LineProbeDiagnosticLevel diagnosticLevel)
        {
            if (diagnosticLevel == LineProbeDiagnosticLevel.Minimal)
            {
                return BuildMinimalDiagnostics(sourceFile, lineNum, probeId);
            }

            return new LineProbeResolutionDiagnostics(
                ProbeFile: sourceFile,
                ProbeLine: lineNum,
                ResolvedSourceFile: assemblyPathMatch.Path,
                PathMatchType: assemblyPathMatch.PathMatchType,
                MatchingTrailingSegments: assemblyPathMatch.MatchingTrailingSegments,
                AssemblyName: assemblyPathMatch.Assembly.GetName().Name,
                AssemblyLocation: assemblyPathMatch.Assembly.Location,
                ModuleVersionId: assemblyPathMatch.Assembly.ManifestModule.ModuleVersionId,
                ProbeId: probeId);
        }

        private static string BuildAssemblyNotLoadedOrSymbolsUnavailableMessage(bool hasSameFileNameMatches)
        {
            var message = "Source file location for probe could not be matched to any currently loaded assembly with available symbols. This can happen if the relevant assembly is not loaded yet or its symbols are unavailable.";
            if (!hasSameFileNameMatches)
            {
                return message;
            }

            return message + " Loaded symbolicated assemblies with the same file name were found, so the configured source path may differ from the PDB document path.";
        }

        private IList<string>? GetDocumentsFromPDB(Assembly loadedAssembly)
        {
            try
            {
                if (AssemblyFilter.ShouldSkipAssembly(loadedAssembly, _thirdPartyDetectionExcludes, _thirdPartyDetectionIncludes))
                {
                    return null;
                }

                using var reader = DatadogMetadataReader.CreatePdbReader(loadedAssembly);
                return reader?.GetDocuments();
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to retrieve documents from PDB for {AssemblyLocation}", loadedAssembly.Location);
            }

            return null;
        }

        private FilePathLookup? GetSourceFilePathForAssembly(Assembly loadedAssembly)
        {
            if (_loadedAssemblies.TryGetValue(loadedAssembly, out var trie))
            {
                return trie;
            }

            return _loadedAssemblies[loadedAssembly] = CreateLookupForSourceFilePaths(loadedAssembly);
        }

        private FilePathLookup? CreateLookupForSourceFilePaths(Assembly loadedAssembly)
        {
            var documents = GetDocumentsFromPDB(loadedAssembly);
            if (documents == null)
            {
                return null; // No PDB available or unsupported assembly
            }

            var lookup = new FilePathLookup();
            foreach (var symbolDocument in documents)
            {
                lookup.InsertPath(symbolDocument);
            }

            return lookup;
        }

        private bool TryFindAssemblyContainingFile(string probeFilePath, LineProbeDiagnosticLevel diagnosticLevel, [NotNullWhen(true)] out AssemblyPathMatch? assemblyPathMatch, [NotNullWhen(false)] out AssemblySearchDiagnostics? diagnostics)
        {
            assemblyPathMatch = null;
            diagnostics = null;
            var probePathQuery = new ProbePathQuery(probeFilePath);
            var includeDetailedDiagnostics = diagnosticLevel == LineProbeDiagnosticLevel.Full;

            lock (_locker)
            {
                BestFallbackMatch? bestMatch = null;
                var hasAmbiguousBestMatch = false;
                List<string>? sameFileNameMatches = null;
                var loadedAssemblyCount = 0;
                var symbolicatedAssemblyCount = 0;
                var sameFileNameMatchCount = 0;
                var bestMatchingTrailingSegments = 0;
                var qualifiedFallbackMatchCount = 0;

                foreach (var candidateAssembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    loadedAssemblyCount++;
                    var lookup = GetSourceFilePathForAssembly(candidateAssembly);
                    if (lookup is null)
                    {
                        continue;
                    }

                    symbolicatedAssemblyCount++;

                    var exactPath = lookup.FindPathThatEndsWith(probePathQuery);
                    if (exactPath is not null)
                    {
                        assemblyPathMatch = new AssemblyPathMatch(candidateAssembly, exactPath, LineProbePathMatchType.ExactSuffixMatch, matchingTrailingSegments: null);
                        return true;
                    }

                    var closestPathMatch = lookup.GetClosestPathBySuffix(probePathQuery, MinTrailingSegmentsForFallbackMatch);
                    TrackClosestPathMatch(
                        candidateAssembly,
                        closestPathMatch,
                        includeDetailedDiagnostics,
                        ref sameFileNameMatchCount,
                        ref bestMatchingTrailingSegments,
                        ref qualifiedFallbackMatchCount,
                        ref sameFileNameMatches);

                    if (!closestPathMatch.IsSuccessful)
                    {
                        continue;
                    }

                    if (bestMatch is null || closestPathMatch.BestQualifiedMatchingTrailingSegments > bestMatch.MatchingTrailingSegments)
                    {
                        bestMatch = new BestFallbackMatch(candidateAssembly, closestPathMatch.BestQualifiedPath!, closestPathMatch.BestQualifiedMatchingTrailingSegments);
                        hasAmbiguousBestMatch = false;
                    }
                    else if (closestPathMatch.BestQualifiedMatchingTrailingSegments == bestMatch.MatchingTrailingSegments)
                    {
                        hasAmbiguousBestMatch = true;
                    }
                }

                if (!hasAmbiguousBestMatch && bestMatch is not null)
                {
                    assemblyPathMatch = new AssemblyPathMatch(bestMatch.Assembly, bestMatch.Path, LineProbePathMatchType.FallbackTrailingSuffixMatch, bestMatch.MatchingTrailingSegments);
                    return true;
                }

                diagnostics = new AssemblySearchDiagnostics(
                    probeFilePath,
                    loadedAssemblyCount,
                    symbolicatedAssemblyCount,
                    sameFileNameMatchCount,
                    bestMatchingTrailingSegments,
                    qualifiedFallbackMatchCount,
                    hasAmbiguousBestMatch,
                    includeDetailedDiagnostics ? sameFileNameMatches?.ToArray() ?? [] : []);
                return false;
            }
        }

        public LineProbeResolveResult TryResolveLineProbe(ProbeDefinition probe, out BoundLineProbeLocation? location, LineProbeDiagnosticLevel diagnosticLevel = LineProbeDiagnosticLevel.Full)
        {
            location = null;
            try
            {
                var sourceFile = probe.Where?.SourceFile;
                if (sourceFile == null)
                {
                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Error,
                        LineProbeResolveReason.MissingSourceFile,
                        "Source file is empty.",
                        new LineProbeResolutionDiagnostics(ProbeFile: "<empty>", ProbeId: probe.Id));
                }

                if (probe.Where?.Lines?.Length != 1 || !int.TryParse(probe.Where.Lines[0], out var lineNum))
                {
                    var invalidLineDiagnostics = diagnosticLevel == LineProbeDiagnosticLevel.Full
                                                     ? new LineProbeResolutionDiagnostics(
                                                         ProbeFile: sourceFile,
                                                         RawLines: string.Join(",", probe.Where?.Lines ?? Array.Empty<string>()),
                                                         ProbeId: probe.Id)
                                                     : new LineProbeResolutionDiagnostics(ProbeFile: sourceFile, ProbeId: probe.Id);

                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Error,
                        LineProbeResolveReason.InvalidLineNumber,
                        "Failed to parse line number.",
                        invalidLineDiagnostics);
                }

                if (!TryFindAssemblyContainingFile(sourceFile, diagnosticLevel, out var assemblyPathMatch, out var searchDiagnostics))
                {
                    var reason = searchDiagnostics is { SameFileNameMatchCount: > 0 }
                                     ? LineProbeResolveReason.LoadedAssemblySourceFileMismatch
                                     : LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable;
                    var message = searchDiagnostics is { SameFileNameMatchCount: > 0 }
                                      ? BuildLoadedAssemblySourceFileMismatchMessage()
                                      : BuildAssemblyNotLoadedOrSymbolsUnavailableMessage();
                    var unresolvedDiagnostics = diagnosticLevel == LineProbeDiagnosticLevel.Full
                                                    ? searchDiagnostics?.ToDiagnostics(lineNum, probe.Id) ?? BuildMinimalDiagnostics(sourceFile, lineNum, probe.Id)
                                                    : BuildMinimalDiagnostics(sourceFile, lineNum, probe.Id);

                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Unbound,
                        reason,
                        message,
                        unresolvedDiagnostics);
                }

                var filePathFromPdb = assemblyPathMatch.Path;
                var assembly = assemblyPathMatch.Assembly;
                using var pdbReader = DatadogMetadataReader.CreatePdbReader(assembly);
                if (pdbReader is not { IsPdbExist: true })
                {
                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Error,
                        LineProbeResolveReason.MissingPdb,
                        "Failed to read from PDB",
                        BuildResolvedAssemblyDiagnostics(sourceFile, lineNum, assemblyPathMatch, probe.Id, diagnosticLevel));
                }

                var method = pdbReader.GetContainingMethodTokenAndOffset(filePathFromPdb, lineNum, column: null, out var bytecodeOffset);
                if (bytecodeOffset.HasValue == false || method.HasValue == false)
                {
                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Error,
                        LineProbeResolveReason.MissingSequencePoint,
                        "Probe location did not map out to a valid bytecode offset",
                        BuildResolvedAssemblyDiagnostics(sourceFile, lineNum, assemblyPathMatch, probe.Id, diagnosticLevel));
                }

                location = new BoundLineProbeLocation(probe, assembly.ManifestModule.ModuleVersionId, method.Value, bytecodeOffset.Value, lineNum);
                return new LineProbeResolveResult(
                    LiveProbeResolveStatus.Bound,
                    Diagnostics: BuildResolvedAssemblyDiagnostics(sourceFile, lineNum, assemblyPathMatch, probe.Id, diagnosticLevel));
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to resolve line probe for ProbeID {ProbeId}", probe.Id);
                var exceptionDiagnostics = diagnosticLevel == LineProbeDiagnosticLevel.Full
                                               ? new LineProbeResolutionDiagnostics(
                                                   ProbeFile: probe.Where?.SourceFile ?? "<empty>",
                                                   ProbeId: probe.Id,
                                                   ExceptionType: e.GetType().FullName)
                                               : new LineProbeResolutionDiagnostics(
                                                   ProbeFile: probe.Where?.SourceFile ?? "<empty>",
                                                   ProbeId: probe.Id);

                return new LineProbeResolveResult(
                    LiveProbeResolveStatus.Error,
                    LineProbeResolveReason.UnexpectedException,
                    "An error occurred while trying to resolve probe location",
                    exceptionDiagnostics);
            }
        }

        internal readonly struct ProbePathQuery(string path)
        {
            public string Path { get; } = path;

            public string FileName { get; } = GetFileName(path);

            public string ReversedBackslashPath { get; } = FilePathLookup.GetReversePath(path, '\\', trimTrailingSeparators: true);

            public string ReversedForwardSlashPath { get; } = FilePathLookup.GetReversePath(path, '/', trimTrailingSeparators: true);
        }

        internal sealed class FilePathLookup
        {
            private readonly Trie _trie = new();
            private readonly Dictionary<string, List<string>> _documentPathsByFileName = new(StringComparer.OrdinalIgnoreCase);
            private char? _directoryPathSeparator;

            private static char GetDirectoryPathSeparator(string path)
            {
                foreach (var character in path)
                {
                    if (character is '\\' or '/')
                    {
                        return character;
                    }
                }

                return Path.DirectorySeparatorChar;
            }

            public void InsertPath(string path)
            {
                // Note: We purposefully are not supporting the case where the inserted paths contains a mix of both forward and back slashes (e.g. "c:\test\some/path/file.cs")
                // This should be fine because the inserted paths are generated by the compiler, not by humans, so we can assume that they are all consistent.
                _directoryPathSeparator ??= GetDirectoryPathSeparator(path);
                _trie.Insert(GetReversePath(path, _directoryPathSeparator.Value, trimTrailingSeparators: false));
                var fileName = GetFileName(path);
                if (string.IsNullOrEmpty(fileName))
                {
                    return;
                }

                if (!_documentPathsByFileName.TryGetValue(fileName, out var documentPaths))
                {
                    documentPaths = [];
                    _documentPathsByFileName[fileName] = documentPaths;
                }

                documentPaths.Add(path);
            }

            public string? FindPathThatEndsWith(string path)
                => FindPathThatEndsWith(new ProbePathQuery(path));

            public string? FindPathThatEndsWith(ProbePathQuery pathQuery)
            {
                // The trie is built from the PDB, so it will contains absolute paths such as:
                //      "D:\build_agent\yada\yada\src\MyProject\MyFile.cs",
                // ...whereas probeFilePath will typically be a path within the Git repo, such as:
                //      "src/MyProject/MyFile.cs"
                // Because we're actually interested in matching the ending of the string rather than the beginning,
                // we reverse all strings before inserting or querying the trie, and then reverse again when we get the string out.
                var directoryPathSeparator = _directoryPathSeparator ?? Path.DirectorySeparatorChar;
                var reversePath = directoryPathSeparator == '\\' ? pathQuery.ReversedBackslashPath : pathQuery.ReversedForwardSlashPath;
                var match = _trie.GetStringStartingWith(reversePath);
                return match != null ? GetReversePath(match, directoryPathSeparator, trimTrailingSeparators: false) : null;
            }

            public bool TryGetDocumentPathByFileName(string fileName, [NotNullWhen(true)] out string? documentPath)
            {
                if (_documentPathsByFileName.TryGetValue(fileName, out var documentPaths) && documentPaths.Count > 0)
                {
                    documentPath = documentPaths[0];
                    return true;
                }

                documentPath = null;
                return false;
            }

            public bool TryFindClosestPathBySuffix(string path, int minimumMatchingTrailingSegments, [NotNullWhen(true)] out string? documentPath, out int matchingTrailingSegments)
            {
                var result = GetClosestPathBySuffix(new ProbePathQuery(path), minimumMatchingTrailingSegments);
                documentPath = result.IsSuccessful ? result.BestQualifiedPath : null;
                matchingTrailingSegments = result.IsSuccessful ? result.BestQualifiedMatchingTrailingSegments : 0;
                return result.IsSuccessful;
            }

            internal ClosestPathBySuffixResult GetClosestPathBySuffix(string path, int minimumMatchingTrailingSegments)
                => GetClosestPathBySuffix(new ProbePathQuery(path), minimumMatchingTrailingSegments);

            internal ClosestPathBySuffixResult GetClosestPathBySuffix(ProbePathQuery pathQuery, int minimumMatchingTrailingSegments)
            {
                if (string.IsNullOrEmpty(pathQuery.FileName) || !_documentPathsByFileName.TryGetValue(pathQuery.FileName, out var documentPaths))
                {
                    return ClosestPathBySuffixResult.NoCandidates();
                }

                var bestMatchingTrailingSegments = 0;
                var bestQualifiedMatchingTrailingSegments = 0;
                var qualifiedMatchCount = 0;
                var hasAmbiguousBestQualifiedMatch = false;
                string? bestQualifiedPath = null;

                foreach (var candidatePath in documentPaths)
                {
                    var candidateMatchingTrailingSegments = CountMatchingTrailingSegments(pathQuery.Path, candidatePath);
                    if (candidateMatchingTrailingSegments > bestMatchingTrailingSegments)
                    {
                        bestMatchingTrailingSegments = candidateMatchingTrailingSegments;
                    }

                    if (candidateMatchingTrailingSegments < minimumMatchingTrailingSegments)
                    {
                        continue;
                    }

                    qualifiedMatchCount++;
                    if (candidateMatchingTrailingSegments > bestQualifiedMatchingTrailingSegments)
                    {
                        bestQualifiedPath = candidatePath;
                        bestQualifiedMatchingTrailingSegments = candidateMatchingTrailingSegments;
                        hasAmbiguousBestQualifiedMatch = false;
                    }
                    else if (candidateMatchingTrailingSegments == bestQualifiedMatchingTrailingSegments)
                    {
                        hasAmbiguousBestQualifiedMatch = true;
                    }
                }

                return new ClosestPathBySuffixResult(
                    exampleSameFileNamePath: documentPaths[0],
                    bestMatchingTrailingSegments: bestMatchingTrailingSegments,
                    qualifiedMatchCount: qualifiedMatchCount,
                    bestQualifiedMatchingTrailingSegments: bestQualifiedMatchingTrailingSegments,
                    bestQualifiedPath: bestQualifiedPath,
                    hasAmbiguousBestQualifiedMatch: hasAmbiguousBestQualifiedMatch);
            }

            private int CountMatchingTrailingSegments(string path1, string path2)
            {
                var path1Span = path1.AsSpan();
                var path2Span = path2.AsSpan();
                TrimTrailingSeparators(ref path1Span);
                TrimTrailingSeparators(ref path2Span);
                var matchingTrailingSegments = 0;

                while (!path1Span.IsEmpty && !path2Span.IsEmpty)
                {
                    var path1SeparatorIndex = FindLastSeparatorIndex(path1Span);
                    var path2SeparatorIndex = FindLastSeparatorIndex(path2Span);
                    var path1Segment = path1SeparatorIndex >= 0 ? path1Span.Slice(path1SeparatorIndex + 1) : path1Span;
                    var path2Segment = path2SeparatorIndex >= 0 ? path2Span.Slice(path2SeparatorIndex + 1) : path2Span;

                    if (!path1Segment.SequenceEqual(path2Segment))
                    {
                        break;
                    }

                    matchingTrailingSegments++;
                    path1Span = path1SeparatorIndex >= 0 ? path1Span.Slice(0, path1SeparatorIndex) : ReadOnlySpan<char>.Empty;
                    path2Span = path2SeparatorIndex >= 0 ? path2Span.Slice(0, path2SeparatorIndex) : ReadOnlySpan<char>.Empty;
                    TrimTrailingSeparators(ref path1Span);
                    TrimTrailingSeparators(ref path2Span);
                }

                return matchingTrailingSegments;
            }

            internal static string GetReversePath(string documentFullPath, char directorySeparator, bool trimTrailingSeparators)
            {
                var pathSpan = documentFullPath.AsSpan();
                if (trimTrailingSeparators)
                {
                    TrimTrailingSeparators(ref pathSpan);
                }

                if (pathSpan.IsEmpty)
                {
                    return string.Empty;
                }

                var builder = new StringBuilder(pathSpan.Length);
                var segmentEnd = pathSpan.Length;
                var isFirstSegment = true;

                while (segmentEnd >= 0)
                {
                    var separatorIndex = segmentEnd - 1;
                    while (separatorIndex >= 0 && !IsDirectorySeparator(pathSpan[separatorIndex]))
                    {
                        separatorIndex--;
                    }

                    if (!isFirstSegment)
                    {
                        builder.Append(directorySeparator);
                    }

                    var segmentStart = separatorIndex + 1;
                    for (var i = segmentStart; i < segmentEnd; i++)
                    {
                        builder.Append(pathSpan[i]);
                    }

                    if (separatorIndex < 0)
                    {
                        break;
                    }

                    isFirstSegment = false;
                    segmentEnd = separatorIndex;
                }

                return builder.ToString();
            }
        }

        internal sealed class BoundLineProbeLocation(ProbeDefinition probeDefinition, Guid mvid, int methodToken, int bytecodeOffset, int lineNumber)
        {
            public ProbeDefinition ProbeDefinition { get; set; } = probeDefinition;

            public Guid Mvid { get; set; } = mvid;

            public int MethodToken { get; set; } = methodToken;

            public int BytecodeOffset { get; set; } = bytecodeOffset;

            public int LineNumber { get; set; } = lineNumber;
        }

        private sealed class AssemblySearchDiagnostics
        {
            public AssemblySearchDiagnostics(
                string probeFilePath,
                int loadedAssemblyCount,
                int symbolicatedAssemblyCount,
                int sameFileNameMatchCount,
                int bestMatchingTrailingSegments,
                int qualifiedFallbackMatchCount,
                bool hasAmbiguousBestMatch,
                string[] sameFileNameExamples)
            {
                ProbeFilePath = probeFilePath;
                LoadedAssemblyCount = loadedAssemblyCount;
                SymbolicatedAssemblyCount = symbolicatedAssemblyCount;
                SameFileNameMatchCount = sameFileNameMatchCount;
                BestMatchingTrailingSegments = bestMatchingTrailingSegments;
                QualifiedFallbackMatchCount = qualifiedFallbackMatchCount;
                SameFileNameExamples = sameFileNameExamples;
                FallbackFailureReason = sameFileNameMatchCount == 0
                                            ? LineProbeFallbackFailureReason.NoSameFileNameCandidates
                                            : hasAmbiguousBestMatch || qualifiedFallbackMatchCount > 1
                                                ? LineProbeFallbackFailureReason.AmbiguousQualifiedMatches
                                                : LineProbeFallbackFailureReason.NoQualifiedSuffixMatch;
            }

            public string ProbeFilePath { get; }

            public int LoadedAssemblyCount { get; }

            public int SymbolicatedAssemblyCount { get; }

            public int SameFileNameMatchCount { get; }

            public int BestMatchingTrailingSegments { get; }

            public int QualifiedFallbackMatchCount { get; }

            public LineProbeFallbackFailureReason FallbackFailureReason { get; }

            public string[] SameFileNameExamples { get; }

            public LineProbeResolutionDiagnostics ToDiagnostics(int lineNumber, string probeId)
            {
                return new LineProbeResolutionDiagnostics(
                    ProbeFile: ProbeFilePath,
                    ProbeLine: lineNumber,
                    ProbeId: probeId,
                    MatchingTrailingSegments: BestMatchingTrailingSegments > 0 ? BestMatchingTrailingSegments : null,
                    FallbackFailureReason: FallbackFailureReason,
                    QualifiedFallbackMatchCount: QualifiedFallbackMatchCount,
                    LoadedAssemblyCount: LoadedAssemblyCount,
                    SymbolicatedAssemblyCount: SymbolicatedAssemblyCount,
                    SameFileNameMatchCount: SameFileNameMatchCount,
                    SameFileNameExamples: SameFileNameExamples);
            }
        }

        internal sealed class ClosestPathBySuffixResult(
            string? exampleSameFileNamePath,
            int bestMatchingTrailingSegments,
            int qualifiedMatchCount,
            int bestQualifiedMatchingTrailingSegments,
            string? bestQualifiedPath,
            bool hasAmbiguousBestQualifiedMatch)
        {
            public string? ExampleSameFileNamePath { get; } = exampleSameFileNamePath;

            public int BestMatchingTrailingSegments { get; } = bestMatchingTrailingSegments;

            public int QualifiedMatchCount { get; } = qualifiedMatchCount;

            public int BestQualifiedMatchingTrailingSegments { get; } = bestQualifiedMatchingTrailingSegments;

            public string? BestQualifiedPath { get; } = bestQualifiedPath;

            public bool HasAmbiguousBestQualifiedMatch { get; } = hasAmbiguousBestQualifiedMatch;

            public bool IsSuccessful => QualifiedMatchCount > 0 && !HasAmbiguousBestQualifiedMatch && BestQualifiedPath is not null;

            public static ClosestPathBySuffixResult NoCandidates() => new(
                exampleSameFileNamePath: null,
                bestMatchingTrailingSegments: 0,
                qualifiedMatchCount: 0,
                bestQualifiedMatchingTrailingSegments: 0,
                bestQualifiedPath: null,
                hasAmbiguousBestQualifiedMatch: false);
        }

        private sealed class BestFallbackMatch(Assembly assembly, string path, int matchingTrailingSegments)
        {
            public Assembly Assembly { get; } = assembly;

            public string Path { get; } = path;

            public int MatchingTrailingSegments { get; } = matchingTrailingSegments;
        }

        private sealed class AssemblyPathMatch(Assembly assembly, string path, LineProbePathMatchType pathMatchType, int? matchingTrailingSegments)
        {
            public Assembly Assembly { get; } = assembly;

            public string Path { get; } = path;

            public LineProbePathMatchType PathMatchType { get; } = pathMatchType;

            public int? MatchingTrailingSegments { get; } = matchingTrailingSegments;
        }
    }
}
