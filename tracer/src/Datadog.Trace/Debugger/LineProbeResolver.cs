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
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

namespace Datadog.Trace.Debugger
{
    internal class LineProbeResolver : ILineProbeResolver
    {
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

        public LineProbeResolveResult TryResolveLineProbe(ProbeDefinition probe, out BoundLineProbeLocation? location)
        {
            location = null;
            try
            {
                var sourceFile = probe.Where?.SourceFile;
                if (sourceFile == null)
                {
                    return new LineProbeResolveResult(LiveProbeResolveStatus.Error, "Source file is empty.");
                }

                if (!TryFindAssemblyContainingFile(sourceFile, out var filePathFromPdb, out var assembly))
                {
                    return new LineProbeResolveResult(LiveProbeResolveStatus.Unbound, "Source file location for probe was not found, possibly because the relevant assembly was not yet loaded.");
                }

                using var pdbReader = DatadogMetadataReader.CreatePdbReader(assembly);
                if (pdbReader is not { IsPdbExist: true })
                {
                    return new LineProbeResolveResult(LiveProbeResolveStatus.Error, "Failed to read from PDB");
                }

                if (probe.Where?.Lines?.Length != 1 || !int.TryParse(probe.Where.Lines[0], out var lineNum))
                {
                    return new LineProbeResolveResult(LiveProbeResolveStatus.Error, "Failed to parse line number.");
                }

                var method = pdbReader.GetContainingMethodTokenAndOffset(filePathFromPdb, lineNum, column: null, out var bytecodeOffset);
                if (bytecodeOffset.HasValue == false || method.HasValue == false)
                {
                    return new LineProbeResolveResult(LiveProbeResolveStatus.Error, "Probe location did not map out to a valid bytecode offset");
                }

                location = new BoundLineProbeLocation(probe, assembly.ManifestModule.ModuleVersionId, method.Value, bytecodeOffset.Value, lineNum);
                return new LineProbeResolveResult(LiveProbeResolveStatus.Bound);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to resolve line probe for ProbeID {ProbeId}", probe.Id);
                return new LineProbeResolveResult(LiveProbeResolveStatus.Error, "An error occurred while trying to resolve probe location");
            }
        }

        internal class FilePathLookup
        {
            private static readonly string[] DirectorySeparatorsCrossPlatform = { @"\", @"/" };
            private readonly Trie _trie = new();
            private string? _directoryPathSeparator;

            public void InsertPath(string path)
            {
                // Note: We purposefully are not supporting the case where the inserted paths contains a mix of both forward and back slashes (e.g. "c:\test\some/path/file.cs")
                // This should be fine because the inserted paths are generated by the compiler, not by humans, so we can assume that they are all consistent.
                _directoryPathSeparator ??= DirectorySeparatorsCrossPlatform.FirstOrDefault(path.Contains);
                _trie.Insert(GetReversePath(path));
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

            private string GetReversePath(string documentFullPath)
            {
                var partsReverse = documentFullPath.Split(DirectorySeparatorsCrossPlatform, StringSplitOptions.None).Reverse();
                // Preserve the type of slash (back- or forward- slash) that was originally inserted.
                return string.Join(_directoryPathSeparator ?? Path.DirectorySeparatorChar.ToString(), partsReverse);
            }
        }
    }
}
