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
        private static readonly string[] DirectorySeparatorsCrossPlatform = { @"\", @"/" };
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
            return path.Split(DirectorySeparatorsCrossPlatform, StringSplitOptions.None).LastOrDefault() ?? string.Empty;
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

        private bool TryFindAssemblyContainingFile(string probeFilePath, [NotNullWhen(true)] out string? sourceFileFullPathFromPdb, [NotNullWhen(true)] out Assembly? assembly)
        {
            sourceFileFullPathFromPdb = null;
            assembly = null;

            lock (_locker)
            {
                foreach (var candidateAssembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var lookup = GetSourceFilePathForAssembly(candidateAssembly);
                    var path = lookup?.FindPathThatEndsWith(probeFilePath);
                    if (path == null)
                    {
                        continue;
                    }

                    sourceFileFullPathFromPdb = path;
                    assembly = candidateAssembly;
                    return true;
                }
            }

            return false;
        }

        private AssemblySearchDiagnostics CollectAssemblySearchDiagnostics(string probeFilePath)
        {
            var diagnostics = new AssemblySearchDiagnostics(probeFilePath);

            lock (_locker)
            {
                foreach (var candidateAssembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    diagnostics.IncrementLoadedAssemblies();
                    var lookup = GetSourceFilePathForAssembly(candidateAssembly);
                    if (lookup != null)
                    {
                        diagnostics.IncrementSymbolicatedAssemblies();
                        if (lookup.TryGetDocumentPathByFileName(diagnostics.ProbeFileName, out var sameFileNameDocument))
                        {
                            diagnostics.AddSameFileNameMatch(candidateAssembly, sameFileNameDocument);
                        }
                    }
                }
            }

            return diagnostics;
        }

        public LineProbeResolveResult TryResolveLineProbe(ProbeDefinition probe, out BoundLineProbeLocation? location)
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
                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Error,
                        LineProbeResolveReason.InvalidLineNumber,
                        "Failed to parse line number.",
                        new LineProbeResolutionDiagnostics(
                            ProbeFile: sourceFile,
                            RawLines: string.Join(",", probe.Where?.Lines ?? Array.Empty<string>()),
                            ProbeId: probe.Id));
                }

                if (!TryFindAssemblyContainingFile(sourceFile, out var filePathFromPdb, out var assembly))
                {
                    var searchDiagnostics = CollectAssemblySearchDiagnostics(sourceFile);
                    var hasSameFileNameMatches = searchDiagnostics.SameFileNameMatchCount > 0;

                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Unbound,
                        LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable,
                        BuildAssemblyNotLoadedOrSymbolsUnavailableMessage(hasSameFileNameMatches),
                        searchDiagnostics.ToDiagnostics(lineNum, probe.Id));
                }

                using var pdbReader = DatadogMetadataReader.CreatePdbReader(assembly);
                if (pdbReader is not { IsPdbExist: true })
                {
                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Error,
                        LineProbeResolveReason.MissingPdb,
                        "Failed to read from PDB",
                        BuildResolvedAssemblyDiagnostics(sourceFile, lineNum, filePathFromPdb, assembly, probe.Id));
                }

                var method = pdbReader.GetContainingMethodTokenAndOffset(filePathFromPdb, lineNum, column: null, out var bytecodeOffset);
                if (bytecodeOffset.HasValue == false || method.HasValue == false)
                {
                    return new LineProbeResolveResult(
                        LiveProbeResolveStatus.Error,
                        LineProbeResolveReason.MissingSequencePoint,
                        "Probe location did not map out to a valid bytecode offset",
                        BuildResolvedAssemblyDiagnostics(sourceFile, lineNum, filePathFromPdb, assembly, probe.Id));
                }

                location = new BoundLineProbeLocation(probe, assembly.ManifestModule.ModuleVersionId, method.Value, bytecodeOffset.Value, lineNum);
                return new LineProbeResolveResult(
                    LiveProbeResolveStatus.Bound,
                    Diagnostics: BuildResolvedAssemblyDiagnostics(sourceFile, lineNum, filePathFromPdb, assembly, probe.Id));
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to resolve line probe for ProbeID {ProbeId}", probe.Id);
                return new LineProbeResolveResult(
                    LiveProbeResolveStatus.Error,
                    LineProbeResolveReason.UnexpectedException,
                    "An error occurred while trying to resolve probe location",
                    new LineProbeResolutionDiagnostics(
                        ProbeFile: probe.Where?.SourceFile ?? "<empty>",
                        ProbeId: probe.Id,
                        ExceptionType: e.GetType().FullName));
            }
        }

        private static LineProbeResolutionDiagnostics BuildResolvedAssemblyDiagnostics(string sourceFile, int lineNum, string filePathFromPdb, Assembly assembly, string probeId)
        {
            return new LineProbeResolutionDiagnostics(
                ProbeFile: sourceFile,
                ProbeLine: lineNum,
                ResolvedSourceFile: filePathFromPdb,
                AssemblyName: assembly.GetName().Name,
                AssemblyLocation: assembly.Location,
                ModuleVersionId: assembly.ManifestModule.ModuleVersionId,
                ProbeId: probeId);
        }

        internal sealed class FilePathLookup
        {
            private readonly Trie _trie = new();
            private readonly Dictionary<string, string> _documentPathsByFileName = new(StringComparer.OrdinalIgnoreCase);
            private string? _directoryPathSeparator;

            public void InsertPath(string path)
            {
                // Note: We purposefully are not supporting the case where the inserted paths contains a mix of both forward and back slashes (e.g. "c:\test\some/path/file.cs")
                // This should be fine because the inserted paths are generated by the compiler, not by humans, so we can assume that they are all consistent.
                _directoryPathSeparator ??= DirectorySeparatorsCrossPlatform.FirstOrDefault(path.Contains);
                _trie.Insert(GetReversePath(path));
                var fileName = GetFileName(path);
                if (!string.IsNullOrEmpty(fileName) && !_documentPathsByFileName.ContainsKey(fileName))
                {
                    _documentPathsByFileName[fileName] = path;
                }
            }

            public string? FindPathThatEndsWith(string path)
            {
                // The trie is built from the PDB, so it will contains absolute paths such as:
                //      "D:\build_agent\yada\yada\src\MyProject\MyFile.cs",
                // ...whereas probeFilePath will typically be a path within the Git repo, such as:
                //      "src/MyProject/MyFile.cs"
                // Because we're actually interested in matching the ending of the string rather than the beginning,
                // we reverse all strings before inserting or querying the trie, and then reverse again when we get the string out.
                var reversePath = GetReversePath(path);
                var match = _trie.GetStringStartingWith(reversePath);
                return match != null ? GetReversePath(match) : null;
            }

            public bool TryGetDocumentPathByFileName(string fileName, [NotNullWhen(true)] out string? documentPath)
            {
                return _documentPathsByFileName.TryGetValue(fileName, out documentPath);
            }

            private string GetReversePath(string documentFullPath)
            {
                // We hit the `public static void Reverse<T>(this Span<T> span)` function here on .NET 3.1+
                // This causes this to fail to compile.
                // Just specifying .AsEnumerable for now to minimize changes
                var partsReverse = documentFullPath.Split(DirectorySeparatorsCrossPlatform, StringSplitOptions.None).AsEnumerable().Reverse();
                // Preserve the type of slash (back- or forward- slash) that was originally inserted.
                return string.Join(_directoryPathSeparator ?? Path.DirectorySeparatorChar.ToString(), partsReverse);
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
            private const int MaxExamples = 3;
            private readonly List<string> _sameFileNameMatches = new();

            public AssemblySearchDiagnostics(string probeFilePath)
            {
                ProbeFilePath = probeFilePath;
                ProbeFileName = GetFileName(probeFilePath);
            }

            public string ProbeFilePath { get; }

            public string ProbeFileName { get; }

            public int LoadedAssemblyCount { get; private set; }

            public int SymbolicatedAssemblyCount { get; private set; }

            public int SameFileNameMatchCount { get; private set; }

            public void IncrementLoadedAssemblies() => LoadedAssemblyCount++;

            public void IncrementSymbolicatedAssemblies() => SymbolicatedAssemblyCount++;

            public void AddSameFileNameMatch(Assembly assembly, string documentPath)
            {
                SameFileNameMatchCount++;
                if (_sameFileNameMatches.Count < MaxExamples)
                {
                    _sameFileNameMatches.Add($"{assembly.GetName().Name}:{documentPath}");
                }
            }

            public LineProbeResolutionDiagnostics ToDiagnostics(int lineNumber, string probeId)
            {
                return new LineProbeResolutionDiagnostics(
                    ProbeFile: ProbeFilePath,
                    ProbeLine: lineNumber,
                    ProbeId: probeId,
                    LoadedAssemblyCount: LoadedAssemblyCount,
                    SymbolicatedAssemblyCount: SymbolicatedAssemblyCount,
                    SameFileNameMatchCount: SameFileNameMatchCount,
                    SameFileNameExamples: _sameFileNameMatches.ToArray());
            }
        }
    }
}
