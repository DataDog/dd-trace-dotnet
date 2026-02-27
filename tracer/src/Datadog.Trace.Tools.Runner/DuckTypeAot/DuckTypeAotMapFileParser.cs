// <copyright file="DuckTypeAotMapFileParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal static class DuckTypeAotMapFileParser
    {
        internal static DuckTypeAotMapFileParseResult Parse(string path)
        {
            var errors = new List<string>();
            var mappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);
            var excludedKeys = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(path))
            {
                return new DuckTypeAotMapFileParseResult(Array.Empty<DuckTypeAotMapping>(), excludedKeys, errors);
            }

            try
            {
                var json = File.ReadAllText(path);
                var parsedFile = JsonConvert.DeserializeObject<MapFileDocument>(json);
                if (parsedFile is null)
                {
                    errors.Add($"--map-file content is empty or invalid JSON: {path}");
                    return new DuckTypeAotMapFileParseResult(Array.Empty<DuckTypeAotMapping>(), excludedKeys, errors);
                }

                ParseEntries(parsedFile.Mappings, path, mappings, excludedKeys, errors);
                ParseEntries(parsedFile.Excludes, path, mappings, excludedKeys, errors, forceExclude: true);
            }
            catch (Exception ex)
            {
                errors.Add($"--map-file could not be parsed ({path}): {ex.Message}");
            }

            return new DuckTypeAotMapFileParseResult(mappings.Values, excludedKeys, errors);
        }

        private static void ParseEntries(
            IReadOnlyList<MapEntry>? entries,
            string path,
            IDictionary<string, DuckTypeAotMapping> mappings,
            ISet<string> excludedKeys,
            ICollection<string> errors,
            bool forceExclude = false)
        {
            if (entries is null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!TryParseEntry(entry, path, i, errors, out var mapping, out var shouldExclude))
                {
                    continue;
                }

                if (forceExclude || shouldExclude)
                {
                    excludedKeys.Add(mapping.Key);
                    _ = mappings.Remove(mapping.Key);
                    continue;
                }

                mappings[mapping.Key] = mapping;
            }
        }

        private static bool TryParseEntry(
            MapEntry entry,
            string path,
            int index,
            ICollection<string> errors,
            out DuckTypeAotMapping mapping,
            out bool exclude)
        {
            mapping = null!;
            exclude = entry.Exclude ?? false;

            var mode = ParseMode(entry.Mode, path, index, errors);
            if (mode is null)
            {
                return false;
            }

            var (proxyTypeFromQualifiedName, proxyAssemblyFromQualifiedName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(entry.ProxyType ?? string.Empty);
            var (targetTypeFromQualifiedName, targetAssemblyFromQualifiedName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(entry.TargetType ?? string.Empty);

            var proxyType = proxyTypeFromQualifiedName;
            var targetType = targetTypeFromQualifiedName;
            var proxyAssembly = DuckTypeAotNameHelpers.NormalizeAssemblyName(entry.ProxyAssembly ?? proxyAssemblyFromQualifiedName ?? string.Empty);
            var targetAssembly = DuckTypeAotNameHelpers.NormalizeAssemblyName(entry.TargetAssembly ?? targetAssemblyFromQualifiedName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(proxyType) ||
                string.IsNullOrWhiteSpace(proxyAssembly) ||
                string.IsNullOrWhiteSpace(targetType) ||
                string.IsNullOrWhiteSpace(targetAssembly))
            {
                errors.Add($"--map-file entry #{index + 1} in '{path}' must provide proxy/target type and assembly values.");
                return false;
            }

            mapping = new DuckTypeAotMapping(
                proxyType,
                proxyAssembly,
                targetType,
                targetAssembly,
                mode.Value,
                DuckTypeAotMappingSource.MapFile,
                entry.ScenarioId);
            return true;
        }

        private static DuckTypeAotMappingMode? ParseMode(string? mode, string path, int index, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "forward", StringComparison.OrdinalIgnoreCase))
            {
                return DuckTypeAotMappingMode.Forward;
            }

            if (string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase))
            {
                return DuckTypeAotMappingMode.Reverse;
            }

            errors.Add($"--map-file entry #{index + 1} in '{path}' has unsupported mode '{mode}'. Allowed values are 'forward' and 'reverse'.");
            return null;
        }

        private sealed class MapFileDocument
        {
            [JsonProperty("mappings")]
            public List<MapEntry>? Mappings { get; set; }

            [JsonProperty("excludes")]
            public List<MapEntry>? Excludes { get; set; }
        }

        private sealed class MapEntry
        {
            [JsonProperty("proxyType")]
            public string? ProxyType { get; set; }

            [JsonProperty("proxyAssembly")]
            public string? ProxyAssembly { get; set; }

            [JsonProperty("targetType")]
            public string? TargetType { get; set; }

            [JsonProperty("targetAssembly")]
            public string? TargetAssembly { get; set; }

            [JsonProperty("mode")]
            public string? Mode { get; set; }

            [JsonProperty("exclude")]
            public bool? Exclude { get; set; }

            [JsonProperty("scenarioId")]
            public string? ScenarioId { get; set; }
        }
    }

    internal static class DuckTypeAotMappingCatalogParser
    {
        internal static DuckTypeAotMappingCatalogParseResult Parse(string path)
        {
            var errors = new List<string>();
            var requiredMappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(path))
            {
                return new DuckTypeAotMappingCatalogParseResult(Array.Empty<DuckTypeAotMapping>(), errors);
            }

            try
            {
                var json = File.ReadAllText(path);
                var parsedFile = JsonConvert.DeserializeObject<MappingCatalogDocument>(json);
                if (parsedFile?.RequiredMappings is null)
                {
                    errors.Add($"--mapping-catalog content is empty or invalid JSON: {path}");
                    return new DuckTypeAotMappingCatalogParseResult(Array.Empty<DuckTypeAotMapping>(), errors);
                }

                for (var i = 0; i < parsedFile.RequiredMappings.Count; i++)
                {
                    var entry = parsedFile.RequiredMappings[i];
                    if (!TryParseCatalogEntry(entry, path, i, errors, out var mapping))
                    {
                        continue;
                    }

                    requiredMappings[mapping.Key] = mapping;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"--mapping-catalog could not be parsed ({path}): {ex.Message}");
            }

            return new DuckTypeAotMappingCatalogParseResult(requiredMappings.Values, errors);
        }

        private static bool TryParseCatalogEntry(
            CatalogEntry entry,
            string path,
            int index,
            ICollection<string> errors,
            out DuckTypeAotMapping mapping)
        {
            mapping = null!;

            var mode = ParseMode(entry.Mode, path, index, errors);
            if (mode is null)
            {
                return false;
            }

            var (proxyTypeFromQualifiedName, proxyAssemblyFromQualifiedName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(entry.ProxyType ?? string.Empty);
            var (targetTypeFromQualifiedName, targetAssemblyFromQualifiedName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(entry.TargetType ?? string.Empty);

            var proxyType = proxyTypeFromQualifiedName;
            var targetType = targetTypeFromQualifiedName;
            var proxyAssembly = DuckTypeAotNameHelpers.NormalizeAssemblyName(entry.ProxyAssembly ?? proxyAssemblyFromQualifiedName ?? string.Empty);
            var targetAssembly = DuckTypeAotNameHelpers.NormalizeAssemblyName(entry.TargetAssembly ?? targetAssemblyFromQualifiedName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(proxyType) ||
                string.IsNullOrWhiteSpace(proxyAssembly) ||
                string.IsNullOrWhiteSpace(targetType) ||
                string.IsNullOrWhiteSpace(targetAssembly))
            {
                errors.Add($"--mapping-catalog entry #{index + 1} in '{path}' must provide proxy/target type and assembly values.");
                return false;
            }

            mapping = new DuckTypeAotMapping(
                proxyType,
                proxyAssembly,
                targetType,
                targetAssembly,
                mode.Value,
                DuckTypeAotMappingSource.MapFile,
                entry.ScenarioId);
            return true;
        }

        private static DuckTypeAotMappingMode? ParseMode(string? mode, string path, int index, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "forward", StringComparison.OrdinalIgnoreCase))
            {
                return DuckTypeAotMappingMode.Forward;
            }

            if (string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase))
            {
                return DuckTypeAotMappingMode.Reverse;
            }

            errors.Add($"--mapping-catalog entry #{index + 1} in '{path}' has unsupported mode '{mode}'. Allowed values are 'forward' and 'reverse'.");
            return null;
        }

        private sealed class MappingCatalogDocument
        {
            [JsonProperty("requiredMappings")]
            public List<CatalogEntry>? RequiredMappings { get; set; }
        }

        private sealed class CatalogEntry
        {
            [JsonProperty("proxyType")]
            public string? ProxyType { get; set; }

            [JsonProperty("proxyAssembly")]
            public string? ProxyAssembly { get; set; }

            [JsonProperty("targetType")]
            public string? TargetType { get; set; }

            [JsonProperty("targetAssembly")]
            public string? TargetAssembly { get; set; }

            [JsonProperty("mode")]
            public string? Mode { get; set; }

            [JsonProperty("scenarioId")]
            public string? ScenarioId { get; set; }
        }
    }

    internal sealed class DuckTypeAotMapFileParseResult
    {
        public DuckTypeAotMapFileParseResult(IEnumerable<DuckTypeAotMapping> mappings, HashSet<string> excludedKeys, IReadOnlyList<string> errors)
        {
            Mappings = new List<DuckTypeAotMapping>(mappings);
            ExcludedKeys = excludedKeys;
            Errors = errors;
        }

        public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }

        public HashSet<string> ExcludedKeys { get; }

        public IReadOnlyList<string> Errors { get; }
    }

    internal sealed class DuckTypeAotMappingCatalogParseResult
    {
        public DuckTypeAotMappingCatalogParseResult(IEnumerable<DuckTypeAotMapping> requiredMappings, IReadOnlyList<string> errors)
        {
            RequiredMappings = new List<DuckTypeAotMapping>(requiredMappings);
            Errors = errors;
        }

        public IReadOnlyList<DuckTypeAotMapping> RequiredMappings { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
