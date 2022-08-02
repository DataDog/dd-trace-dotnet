// <copyright file="LineProbeResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

namespace Datadog.Trace.Debugger
{
    internal class LineProbeResolver : ILineProbeResolver
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LineProbeResolver>();
        private static readonly string[] DirectorySeparatorsCrossPlatform = { @"\", @"/" };

        private readonly object _locker;
        private readonly Dictionary<Assembly, Trie> _loadedAssemblies;

        private LineProbeResolver()
        {
            _locker = new object();
            _loadedAssemblies = new Dictionary<Assembly, Trie>();
        }

        public static LineProbeResolver Create()
        {
            return new LineProbeResolver();
        }

        private static IList<SymbolDocument> GetDocumentsFromPDB(Assembly loadedAssembly)
        {
            try
            {
                if (loadedAssembly.IsDynamic ||
                    loadedAssembly.ManifestModule.IsResource() ||
                    string.IsNullOrWhiteSpace(loadedAssembly.Location) ||
                    IsThirdPartyCode(loadedAssembly))
                {
                    return null;
                }

                using var reader = DatadogPdbReader.CreatePdbReader(loadedAssembly);
                if (reader != null)
                {
                    return reader.GetDocuments();
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, $"Failed to retrieve documents from PDB for {loadedAssembly.Location}");
            }

            return null;
        }

        private static bool IsThirdPartyCode(Assembly loadedAssembly)
        {
            // This implementation is just a stub - we will need to replace it
            // with a proper implementation in the future.
            string[] thirdPartyStartsWith = { "Microsoft", "System" };

            var assemblyName = loadedAssembly.GetName().Name;
            return thirdPartyStartsWith.Any(t => assemblyName.StartsWith(t));
        }

        private static string GetReversePath(string documentFullPath)
        {
            var partsReverse = documentFullPath.Split(DirectorySeparatorsCrossPlatform, StringSplitOptions.None).Reverse();
            return string.Join(Path.DirectorySeparatorChar.ToString(), partsReverse);
        }

        private Trie GetSourceFilePathForAssembly(Assembly loadedAssembly)
        {
            if (_loadedAssemblies.TryGetValue(loadedAssembly, out var trie))
            {
                return trie;
            }

            return _loadedAssemblies[loadedAssembly] = CreateTrieForSourceFilePaths(loadedAssembly);
        }

        private Trie CreateTrieForSourceFilePaths(Assembly loadedAssembly)
        {
            var documents = GetDocumentsFromPDB(loadedAssembly);
            if (documents == null)
            {
                return null; // No PDB available or unsupported assembly
            }

            var trie = new Trie();
            foreach (var symbolDocument in documents)
            {
                trie.Insert(GetReversePath(symbolDocument.URL));
            }

            return trie;
        }

        private bool TryFindAssemblyContainingFile(string sourceFileFullPath, out string sourceFileFullPathFromPdb, out Assembly assembly)
        {
            lock (_locker)
            {
                foreach (var candidateAssembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var trie = GetSourceFilePathForAssembly(candidateAssembly);
                    if (trie == null)
                    {
                        continue;
                    }

                    // Because we're actually interested in matching the ending of the string rather than the beginning,
                    // we reverse the string before inserting or querying the trie, and then reverse it again when we get a string out.
                    var reversedPath = trie.GetStringStartingWith(GetReversePath(sourceFileFullPath));
                    if (reversedPath != null)
                    {
                        sourceFileFullPathFromPdb = GetReversePath(reversedPath);
                        assembly = candidateAssembly;
                        return true;
                    }
                }
            }

            sourceFileFullPathFromPdb = null;
            assembly = null;
            return false;
        }

        public LineProbeResolveResult TryResolveLineProbe(ProbeDefinition probe, out BoundLineProbeLocation location)
        {
            location = null;
            if (!TryFindAssemblyContainingFile(probe.Where.SourceFile, out var filePathFromPdb, out var assembly))
            {
                Log.Information("Source file location for probe {ProbeId} was not found, possibly because the relevant assembly was not yet loaded.", probe.Id);
                return new LineProbeResolveResult(LiveProbeResolveStatus.Unbound);
            }

            using var pdbReader = DatadogPdbReader.CreatePdbReader(assembly);
            if (pdbReader == null)
            {
                var message = $"Failed to read from PDB for probe ID {probe.Id}";
                Log.Information(message);

                return new LineProbeResolveResult(LiveProbeResolveStatus.Error, message);
            }

            if (probe.Where.Lines?.Length != 1 || !int.TryParse(probe.Where.Lines[0], out var lineNum))
            {
                var message = $"Failed to parse line number for Line Probe {probe.Id}. " +
                              $"The Lines collection contains {PrintContents(probe.Where.Lines)}.";
                Log.Warning(message);

                return new LineProbeResolveResult(LiveProbeResolveStatus.Error, message);
            }

            var method = pdbReader.GetContainingMethodAndOffset(filePathFromPdb, lineNum, column: null, out var bytecodeOffset);
            if (bytecodeOffset.HasValue == false)
            {
                var message = $"Probe location did not map out to a valid bytecode offset for probe id {probe.Id}";
                Log.Warning(message);
                return new LineProbeResolveResult(LiveProbeResolveStatus.Error, message);
            }

            location = new BoundLineProbeLocation(probe, assembly.ManifestModule.ModuleVersionId, method.Token, bytecodeOffset.Value, lineNum);
            return new LineProbeResolveResult(LiveProbeResolveStatus.Bound);

            string PrintContents<T>(T[] array)
            {
                const string separator = ", ";
                return array == null ? "null" : $"[{string.Join(separator, array)}]";
            }
        }

        public void OnDomainUnloaded()
        {
            lock (_locker)
            {
                foreach (var unloadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    _loadedAssemblies.Remove(unloadedAssembly);
                }
            }
        }
    }
}
