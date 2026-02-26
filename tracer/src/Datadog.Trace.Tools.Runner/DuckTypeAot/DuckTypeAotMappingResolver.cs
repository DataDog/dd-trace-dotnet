// <copyright file="DuckTypeAotMappingResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal static class DuckTypeAotMappingResolver
    {
        internal static DuckTypeAotMappingResolutionResult Resolve(DuckTypeAotGenerateOptions options)
        {
            var warnings = new List<string>();
            var errors = new List<string>();

            var proxyAssemblyPathsByName = BuildAssemblyPathIndex(options.ProxyAssemblies, "--proxy-assembly", errors);
            var targetAssemblyPathsByName = BuildAssemblyPathIndex(GetTargetAssemblyPaths(options), "--target-assembly/--target-folder", errors);

            var resolvedMappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);

            var attributeMappingsResult = DuckTypeAotAttributeDiscovery.Discover(options.ProxyAssemblies);
            warnings.AddRange(attributeMappingsResult.Warnings);
            errors.AddRange(attributeMappingsResult.Errors);
            foreach (var mapping in attributeMappingsResult.Mappings)
            {
                resolvedMappings[mapping.Key] = mapping;
            }

            if (!string.IsNullOrWhiteSpace(options.MapFile))
            {
                var mapFileResult = DuckTypeAotMapFileParser.Parse(options.MapFile!);
                errors.AddRange(mapFileResult.Errors);
                foreach (var mapping in mapFileResult.Mappings)
                {
                    resolvedMappings[mapping.Key] = mapping;
                }

                foreach (var excludedKey in mapFileResult.ExcludedKeys)
                {
                    _ = resolvedMappings.Remove(excludedKey);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.MappingCatalog))
            {
                var catalogResult = DuckTypeAotMappingCatalogParser.Parse(options.MappingCatalog!);
                errors.AddRange(catalogResult.Errors);
                foreach (var requiredMapping in catalogResult.RequiredMappings)
                {
                    if (!resolvedMappings.ContainsKey(requiredMapping.Key))
                    {
                        errors.Add($"Required mapping is missing: mode={requiredMapping.Mode}, proxy={requiredMapping.ProxyTypeName}, target={requiredMapping.TargetTypeName}.");
                    }
                }
            }

            foreach (var mapping in resolvedMappings.Values)
            {
                if (!proxyAssemblyPathsByName.ContainsKey(mapping.ProxyAssemblyName))
                {
                    errors.Add($"Mapping proxy assembly '{mapping.ProxyAssemblyName}' could not be resolved from --proxy-assembly inputs.");
                }

                if (!targetAssemblyPathsByName.ContainsKey(mapping.TargetAssemblyName))
                {
                    errors.Add($"Mapping target assembly '{mapping.TargetAssemblyName}' could not be resolved from --target-assembly/--target-folder inputs.");
                }
            }

            return new DuckTypeAotMappingResolutionResult(
                resolvedMappings.Values,
                proxyAssemblyPathsByName,
                targetAssemblyPathsByName,
                warnings,
                errors);
        }

        private static IReadOnlyList<string> GetTargetAssemblyPaths(DuckTypeAotGenerateOptions options)
        {
            var targetAssemblyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var targetAssembly in options.TargetAssemblies)
            {
                _ = targetAssemblyPaths.Add(targetAssembly);
            }

            foreach (var targetFolder in options.TargetFolders)
            {
                foreach (var targetFilter in options.TargetFilters)
                {
                    foreach (var targetAssemblyPath in Directory.EnumerateFiles(targetFolder, targetFilter, SearchOption.TopDirectoryOnly))
                    {
                        _ = targetAssemblyPaths.Add(targetAssemblyPath);
                    }
                }
            }

            return targetAssemblyPaths.ToList();
        }

        private static Dictionary<string, string> BuildAssemblyPathIndex(IReadOnlyList<string> assemblyPaths, string sourceName, ICollection<string> errors)
        {
            var assemblyPathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assemblyPath in assemblyPaths)
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                    var normalizedAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(assemblyName.Name ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(normalizedAssemblyName))
                    {
                        errors.Add($"{sourceName} could not read assembly name from '{assemblyPath}'.");
                        continue;
                    }

                    if (!assemblyPathByName.TryAdd(normalizedAssemblyName, assemblyPath))
                    {
                        var existingPath = assemblyPathByName[normalizedAssemblyName];
                        if (!string.Equals(existingPath, assemblyPath, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"{sourceName} has duplicate assembly identity '{normalizedAssemblyName}' with different paths: '{existingPath}' and '{assemblyPath}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{sourceName} failed to read assembly metadata for '{assemblyPath}': {ex.Message}");
                }
            }

            return assemblyPathByName;
        }
    }

    internal sealed class DuckTypeAotMappingResolutionResult
    {
        public DuckTypeAotMappingResolutionResult(
            IEnumerable<DuckTypeAotMapping> mappings,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> errors)
        {
            Mappings = new List<DuckTypeAotMapping>(mappings);
            ProxyAssemblyPathsByName = proxyAssemblyPathsByName;
            TargetAssemblyPathsByName = targetAssemblyPathsByName;
            Warnings = warnings;
            Errors = errors;
        }

        public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }

        public IReadOnlyDictionary<string, string> ProxyAssemblyPathsByName { get; }

        public IReadOnlyDictionary<string, string> TargetAssemblyPathsByName { get; }

        public IReadOnlyList<string> Warnings { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
