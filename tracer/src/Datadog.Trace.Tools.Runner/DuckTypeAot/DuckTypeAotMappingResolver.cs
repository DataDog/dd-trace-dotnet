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
            var genericTypeRoots = new Dictionary<string, DuckTypeAotTypeReference>(StringComparer.Ordinal);

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
                ApplyMappingCatalogRequirements(resolvedMappings, catalogResult.RequiredMappings, errors);
            }

            if (!string.IsNullOrWhiteSpace(options.GenericInstantiationsFile))
            {
                var genericInstantiationsResult = DuckTypeAotGenericInstantiationsParser.Parse(options.GenericInstantiationsFile!);
                errors.AddRange(genericInstantiationsResult.Errors);
                foreach (var typeRoot in genericInstantiationsResult.TypeRoots)
                {
                    genericTypeRoots[typeRoot.Key] = typeRoot;
                }
            }

            ValidateGenericClosure(resolvedMappings.Values, errors);
            ValidateScenarioIds(resolvedMappings.Values, errors);

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
                genericTypeRoots.Values,
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

        private static void ValidateGenericClosure(IEnumerable<DuckTypeAotMapping> mappings, ICollection<string> errors)
        {
            foreach (var mapping in mappings)
            {
                if (!DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.ProxyTypeName) &&
                    !DuckTypeAotNameHelpers.IsOpenGenericTypeName(mapping.TargetTypeName))
                {
                    continue;
                }

                errors.Add(
                    $"Mapping '{mapping.Key}' contains an open generic type. " +
                    "NativeAOT generation requires closed proxy and target types. " +
                    "Provide closed concrete mappings in --map-file and use --generic-instantiations for additional closed-generic roots.");
            }
        }

        private static void ApplyMappingCatalogRequirements(
            IDictionary<string, DuckTypeAotMapping> resolvedMappings,
            IEnumerable<DuckTypeAotMapping> requiredMappings,
            ICollection<string> errors)
        {
            foreach (var requiredMapping in requiredMappings)
            {
                if (!resolvedMappings.TryGetValue(requiredMapping.Key, out var resolvedMapping))
                {
                    var scenarioSuffix = string.IsNullOrWhiteSpace(requiredMapping.ScenarioId)
                                             ? string.Empty
                                             : $", scenario={requiredMapping.ScenarioId}";
                    errors.Add(
                        $"Required mapping is missing: mode={requiredMapping.Mode}, proxy={requiredMapping.ProxyTypeName}, target={requiredMapping.TargetTypeName}{scenarioSuffix}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(requiredMapping.ScenarioId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(resolvedMapping.ScenarioId))
                {
                    resolvedMappings[requiredMapping.Key] = resolvedMapping.WithScenarioId(requiredMapping.ScenarioId!);
                    continue;
                }

                if (!string.Equals(resolvedMapping.ScenarioId, requiredMapping.ScenarioId, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Scenario id mismatch for mapping '{requiredMapping.Key}'. " +
                        $"Resolved='{resolvedMapping.ScenarioId}', catalog='{requiredMapping.ScenarioId}'.");
                }
            }
        }

        private static void ValidateScenarioIds(IEnumerable<DuckTypeAotMapping> mappings, ICollection<string> errors)
        {
            var mappingKeyByScenarioId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var mapping in mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.ScenarioId))
                {
                    continue;
                }

                if (mappingKeyByScenarioId.TryGetValue(mapping.ScenarioId!, out var existingMappingKey))
                {
                    if (!string.Equals(existingMappingKey, mapping.Key, StringComparison.Ordinal))
                    {
                        errors.Add(
                            $"Duplicate scenario id '{mapping.ScenarioId}' is assigned to multiple mappings: '{existingMappingKey}' and '{mapping.Key}'.");
                    }

                    continue;
                }

                mappingKeyByScenarioId[mapping.ScenarioId!] = mapping.Key;
            }
        }
    }

    internal sealed class DuckTypeAotMappingResolutionResult
    {
        public DuckTypeAotMappingResolutionResult(
            IEnumerable<DuckTypeAotMapping> mappings,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            IEnumerable<DuckTypeAotTypeReference> genericTypeRoots,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> errors)
        {
            Mappings = new List<DuckTypeAotMapping>(mappings);
            ProxyAssemblyPathsByName = proxyAssemblyPathsByName;
            TargetAssemblyPathsByName = targetAssemblyPathsByName;
            GenericTypeRoots = new List<DuckTypeAotTypeReference>(genericTypeRoots);
            Warnings = warnings;
            Errors = errors;
        }

        public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }

        public IReadOnlyDictionary<string, string> ProxyAssemblyPathsByName { get; }

        public IReadOnlyDictionary<string, string> TargetAssemblyPathsByName { get; }

        public IReadOnlyList<DuckTypeAotTypeReference> GenericTypeRoots { get; }

        public IReadOnlyList<string> Warnings { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
