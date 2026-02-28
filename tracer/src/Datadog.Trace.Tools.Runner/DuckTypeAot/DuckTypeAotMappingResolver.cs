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
    /// <summary>
    /// Provides helper operations for duck type aot mapping resolver.
    /// </summary>
    internal static class DuckTypeAotMappingResolver
    {
        /// <summary>
        /// Resolves resolve.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The result produced by this operation.</returns>
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
                if (options.RequireMappingCatalog && catalogResult.RequiredMappings.Count == 0)
                {
                    errors.Add("--mapping-catalog must contain at least one entry in requiredMappings when --require-mapping-catalog is enabled.");
                }

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

        /// <summary>
        /// Gets get target assembly paths.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The result produced by this operation.</returns>
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

        /// <summary>
        /// Executes build assembly path index.
        /// </summary>
        /// <param name="assemblyPaths">The assembly paths value.</param>
        /// <param name="sourceName">The source name value.</param>
        /// <param name="errors">The errors value.</param>
        /// <returns>The result produced by this operation.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
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

        /// <summary>
        /// Validates validate generic closure.
        /// </summary>
        /// <param name="mappings">The mappings value.</param>
        /// <param name="errors">The errors value.</param>
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

        /// <summary>
        /// Executes apply mapping catalog requirements.
        /// </summary>
        /// <param name="resolvedMappings">The resolved mappings value.</param>
        /// <param name="requiredMappings">The required mappings value.</param>
        /// <param name="errors">The errors value.</param>
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
    }

    /// <summary>
    /// Represents duck type aot mapping resolution result.
    /// </summary>
    internal sealed class DuckTypeAotMappingResolutionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotMappingResolutionResult"/> class.
        /// </summary>
        /// <param name="mappings">The mappings value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="genericTypeRoots">The generic type roots value.</param>
        /// <param name="warnings">The warnings value.</param>
        /// <param name="errors">The errors value.</param>
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

        /// <summary>
        /// Gets mappings.
        /// </summary>
        /// <value>The mappings value.</value>
        public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }

        /// <summary>
        /// Gets proxy assembly paths by name.
        /// </summary>
        /// <value>The proxy assembly paths by name value.</value>
        public IReadOnlyDictionary<string, string> ProxyAssemblyPathsByName { get; }

        /// <summary>
        /// Gets target assembly paths by name.
        /// </summary>
        /// <value>The target assembly paths by name value.</value>
        public IReadOnlyDictionary<string, string> TargetAssemblyPathsByName { get; }

        /// <summary>
        /// Gets generic type roots.
        /// </summary>
        /// <value>The generic type roots value.</value>
        public IReadOnlyList<DuckTypeAotTypeReference> GenericTypeRoots { get; }

        /// <summary>
        /// Gets warnings.
        /// </summary>
        /// <value>The warnings value.</value>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Gets errors.
        /// </summary>
        /// <value>The errors value.</value>
        public IReadOnlyList<string> Errors { get; }
    }
}
