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
    /// <summary>
    /// Provides helper operations for duck type aot map file parser.
    /// </summary>
    internal static class DuckTypeAotMapFileParser
    {
        /// <summary>
        /// Parses parse.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckTypeAotMapFileParseResult Parse(string path)
        {
            var errors = new List<string>();
            var mappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);
            var excludedKeys = new HashSet<string>(StringComparer.Ordinal);

            // Branch: take this path when (string.IsNullOrWhiteSpace(path)) evaluates to true.
            if (string.IsNullOrWhiteSpace(path))
            {
                return new DuckTypeAotMapFileParseResult(Array.Empty<DuckTypeAotMapping>(), excludedKeys, errors);
            }

            try
            {
                var json = File.ReadAllText(path);
                var parsedFile = JsonConvert.DeserializeObject<MapFileDocument>(json);
                // Branch: take this path when (parsedFile is null) evaluates to true.
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
                // Branch: handles exceptions that match Exception ex.
                errors.Add($"--map-file could not be parsed ({path}): {ex.Message}");
            }

            return new DuckTypeAotMapFileParseResult(mappings.Values, excludedKeys, errors);
        }

        /// <summary>
        /// Parses parse entries.
        /// </summary>
        /// <param name="entries">The entries value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="mappings">The mappings value.</param>
        /// <param name="excludedKeys">The excluded keys value.</param>
        /// <param name="errors">The errors value.</param>
        /// <param name="forceExclude">The force exclude value.</param>
        private static void ParseEntries(
            IReadOnlyList<MapEntry>? entries,
            string path,
            IDictionary<string, DuckTypeAotMapping> mappings,
            ISet<string> excludedKeys,
            ICollection<string> errors,
            bool forceExclude = false)
        {
            // Branch: take this path when (entries is null) evaluates to true.
            if (entries is null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                // Branch: take this path when (!TryParseEntry(entry, path, i, errors, out var mapping, out var shouldExclude)) evaluates to true.
                if (!TryParseEntry(entry, path, i, errors, out var mapping, out var shouldExclude))
                {
                    continue;
                }

                // Branch: take this path when (forceExclude || shouldExclude) evaluates to true.
                if (forceExclude || shouldExclude)
                {
                    excludedKeys.Add(mapping.Key);
                    _ = mappings.Remove(mapping.Key);
                    continue;
                }

                mappings[mapping.Key] = mapping;
            }
        }

        /// <summary>
        /// Attempts to try parse entry.
        /// </summary>
        /// <param name="entry">The entry value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="index">The index value.</param>
        /// <param name="errors">The errors value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="exclude">The exclude value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
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
            // Branch: take this path when (mode is null) evaluates to true.
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

            // Branch: take this path when (string.IsNullOrWhiteSpace(proxyType) || evaluates to true.
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
                entry.ScenarioId,
                entry.ExpectCanCreate == false ? DuckTypeAotParityExpectation.CannotCreate : DuckTypeAotParityExpectation.Creatable);
            return true;
        }

        /// <summary>
        /// Parses parse mode.
        /// </summary>
        /// <param name="mode">The mode value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="index">The index value.</param>
        /// <param name="errors">The errors value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingMode? ParseMode(string? mode, string path, int index, ICollection<string> errors)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "forward", StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "forward", StringComparison.OrdinalIgnoreCase))
            {
                return DuckTypeAotMappingMode.Forward;
            }

            // Branch: take this path when (string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase))
            {
                return DuckTypeAotMappingMode.Reverse;
            }

            errors.Add($"--map-file entry #{index + 1} in '{path}' has unsupported mode '{mode}'. Allowed values are 'forward' and 'reverse'.");
            return null;
        }

        /// <summary>
        /// Represents map file document.
        /// </summary>
        private sealed class MapFileDocument
        {
            /// <summary>
            /// Gets or sets mappings.
            /// </summary>
            /// <value>The mappings value.</value>
            [JsonProperty("mappings")]
            public List<MapEntry>? Mappings { get; set; }

            /// <summary>
            /// Gets or sets excludes.
            /// </summary>
            /// <value>The excludes value.</value>
            [JsonProperty("excludes")]
            public List<MapEntry>? Excludes { get; set; }
        }

        /// <summary>
        /// Represents map entry.
        /// </summary>
        private sealed class MapEntry
        {
            /// <summary>
            /// Gets or sets proxy type.
            /// </summary>
            /// <value>The proxy type value.</value>
            [JsonProperty("proxyType")]
            public string? ProxyType { get; set; }

            /// <summary>
            /// Gets or sets proxy assembly.
            /// </summary>
            /// <value>The proxy assembly value.</value>
            [JsonProperty("proxyAssembly")]
            public string? ProxyAssembly { get; set; }

            /// <summary>
            /// Gets or sets target type.
            /// </summary>
            /// <value>The target type value.</value>
            [JsonProperty("targetType")]
            public string? TargetType { get; set; }

            /// <summary>
            /// Gets or sets target assembly.
            /// </summary>
            /// <value>The target assembly value.</value>
            [JsonProperty("targetAssembly")]
            public string? TargetAssembly { get; set; }

            /// <summary>
            /// Gets or sets mode.
            /// </summary>
            /// <value>The mode value.</value>
            [JsonProperty("mode")]
            public string? Mode { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether exclude.
            /// </summary>
            /// <value>The exclude value.</value>
            [JsonProperty("exclude")]
            public bool? Exclude { get; set; }

            /// <summary>
            /// Gets or sets scenario id.
            /// </summary>
            /// <value>The scenario id value.</value>
            [JsonProperty("scenarioId")]
            public string? ScenarioId { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether expect can create.
            /// </summary>
            /// <value>The expect can create value.</value>
            [JsonProperty("expectCanCreate")]
            public bool? ExpectCanCreate { get; set; }
        }
    }

    /// <summary>
    /// Provides helper operations for duck type aot mapping catalog parser.
    /// </summary>
    internal static class DuckTypeAotMappingCatalogParser
    {
        /// <summary>
        /// Parses parse.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <returns>The result produced by this operation.</returns>
        internal static DuckTypeAotMappingCatalogParseResult Parse(string path)
        {
            var errors = new List<string>();
            var requiredMappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);

            // Branch: take this path when (string.IsNullOrWhiteSpace(path)) evaluates to true.
            if (string.IsNullOrWhiteSpace(path))
            {
                return new DuckTypeAotMappingCatalogParseResult(Array.Empty<DuckTypeAotMapping>(), errors);
            }

            try
            {
                var json = File.ReadAllText(path);
                var parsedFile = JsonConvert.DeserializeObject<MappingCatalogDocument>(json);
                // Branch: take this path when (parsedFile?.RequiredMappings is null) evaluates to true.
                if (parsedFile?.RequiredMappings is null)
                {
                    errors.Add($"--mapping-catalog content is empty or invalid JSON: {path}");
                    return new DuckTypeAotMappingCatalogParseResult(Array.Empty<DuckTypeAotMapping>(), errors);
                }

                for (var i = 0; i < parsedFile.RequiredMappings.Count; i++)
                {
                    var entry = parsedFile.RequiredMappings[i];
                    // Branch: take this path when (!TryParseCatalogEntry(entry, path, i, errors, out var mapping)) evaluates to true.
                    if (!TryParseCatalogEntry(entry, path, i, errors, out var mapping))
                    {
                        continue;
                    }

                    requiredMappings[mapping.Key] = mapping;
                }
            }
            catch (Exception ex)
            {
                // Branch: handles exceptions that match Exception ex.
                errors.Add($"--mapping-catalog could not be parsed ({path}): {ex.Message}");
            }

            return new DuckTypeAotMappingCatalogParseResult(requiredMappings.Values, errors);
        }

        /// <summary>
        /// Attempts to try parse catalog entry.
        /// </summary>
        /// <param name="entry">The entry value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="index">The index value.</param>
        /// <param name="errors">The errors value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryParseCatalogEntry(
            CatalogEntry entry,
            string path,
            int index,
            ICollection<string> errors,
            out DuckTypeAotMapping mapping)
        {
            mapping = null!;

            var mode = ParseMode(entry.Mode, path, index, errors);
            // Branch: take this path when (mode is null) evaluates to true.
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

            // Branch: take this path when (string.IsNullOrWhiteSpace(proxyType) || evaluates to true.
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
                entry.ScenarioId,
                entry.ExpectCanCreate == false ? DuckTypeAotParityExpectation.CannotCreate : DuckTypeAotParityExpectation.Creatable);
            return true;
        }

        /// <summary>
        /// Parses parse mode.
        /// </summary>
        /// <param name="mode">The mode value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="index">The index value.</param>
        /// <param name="errors">The errors value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingMode? ParseMode(string? mode, string path, int index, ICollection<string> errors)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "forward", StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "forward", StringComparison.OrdinalIgnoreCase))
            {
                return DuckTypeAotMappingMode.Forward;
            }

            // Branch: take this path when (string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase))
            {
                return DuckTypeAotMappingMode.Reverse;
            }

            errors.Add($"--mapping-catalog entry #{index + 1} in '{path}' has unsupported mode '{mode}'. Allowed values are 'forward' and 'reverse'.");
            return null;
        }

        /// <summary>
        /// Represents mapping catalog document.
        /// </summary>
        private sealed class MappingCatalogDocument
        {
            /// <summary>
            /// Gets or sets required mappings.
            /// </summary>
            /// <value>The required mappings value.</value>
            [JsonProperty("requiredMappings")]
            public List<CatalogEntry>? RequiredMappings { get; set; }
        }

        /// <summary>
        /// Represents catalog entry.
        /// </summary>
        private sealed class CatalogEntry
        {
            /// <summary>
            /// Gets or sets proxy type.
            /// </summary>
            /// <value>The proxy type value.</value>
            [JsonProperty("proxyType")]
            public string? ProxyType { get; set; }

            /// <summary>
            /// Gets or sets proxy assembly.
            /// </summary>
            /// <value>The proxy assembly value.</value>
            [JsonProperty("proxyAssembly")]
            public string? ProxyAssembly { get; set; }

            /// <summary>
            /// Gets or sets target type.
            /// </summary>
            /// <value>The target type value.</value>
            [JsonProperty("targetType")]
            public string? TargetType { get; set; }

            /// <summary>
            /// Gets or sets target assembly.
            /// </summary>
            /// <value>The target assembly value.</value>
            [JsonProperty("targetAssembly")]
            public string? TargetAssembly { get; set; }

            /// <summary>
            /// Gets or sets mode.
            /// </summary>
            /// <value>The mode value.</value>
            [JsonProperty("mode")]
            public string? Mode { get; set; }

            /// <summary>
            /// Gets or sets scenario id.
            /// </summary>
            /// <value>The scenario id value.</value>
            [JsonProperty("scenarioId")]
            public string? ScenarioId { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether expect can create.
            /// </summary>
            /// <value>The expect can create value.</value>
            [JsonProperty("expectCanCreate")]
            public bool? ExpectCanCreate { get; set; }
        }
    }

    /// <summary>
    /// Represents duck type aot map file parse result.
    /// </summary>
    internal sealed class DuckTypeAotMapFileParseResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotMapFileParseResult"/> class.
        /// </summary>
        /// <param name="mappings">The mappings value.</param>
        /// <param name="excludedKeys">The excluded keys value.</param>
        /// <param name="errors">The errors value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        public DuckTypeAotMapFileParseResult(IEnumerable<DuckTypeAotMapping> mappings, HashSet<string> excludedKeys, IReadOnlyList<string> errors)
        {
            Mappings = new List<DuckTypeAotMapping>(mappings);
            ExcludedKeys = excludedKeys;
            Errors = errors;
        }

        /// <summary>
        /// Gets mappings.
        /// </summary>
        /// <value>The mappings value.</value>
        public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }

        /// <summary>
        /// Gets excluded keys.
        /// </summary>
        /// <value>The excluded keys value.</value>
        public HashSet<string> ExcludedKeys { get; }

        /// <summary>
        /// Gets errors.
        /// </summary>
        /// <value>The errors value.</value>
        public IReadOnlyList<string> Errors { get; }
    }

    /// <summary>
    /// Represents duck type aot mapping catalog parse result.
    /// </summary>
    internal sealed class DuckTypeAotMappingCatalogParseResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotMappingCatalogParseResult"/> class.
        /// </summary>
        /// <param name="requiredMappings">The required mappings value.</param>
        /// <param name="errors">The errors value.</param>
        public DuckTypeAotMappingCatalogParseResult(IEnumerable<DuckTypeAotMapping> requiredMappings, IReadOnlyList<string> errors)
        {
            RequiredMappings = new List<DuckTypeAotMapping>(requiredMappings);
            Errors = errors;
        }

        /// <summary>
        /// Gets required mappings.
        /// </summary>
        /// <value>The required mappings value.</value>
        public IReadOnlyList<DuckTypeAotMapping> RequiredMappings { get; }

        /// <summary>
        /// Gets errors.
        /// </summary>
        /// <value>The errors value.</value>
        public IReadOnlyList<string> Errors { get; }
    }
}
