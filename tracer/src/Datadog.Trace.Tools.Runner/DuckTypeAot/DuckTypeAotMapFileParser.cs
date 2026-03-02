// <copyright file="DuckTypeAotMapFileParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

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

            // Branch: take this path when (ContainsDeprecatedExpectCanCreate(entry.AdditionalProperties)) evaluates to true.
            if (ContainsDeprecatedExpectCanCreate(entry.AdditionalProperties))
            {
                errors.Add($"--map-file entry #{index + 1} in '{path}' uses deprecated field 'expectCanCreate'. Strict parity mode requires removing this override field.");
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
        /// Determines whether contains deprecated expect can create.
        /// </summary>
        /// <param name="additionalProperties">The additional properties value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ContainsDeprecatedExpectCanCreate(IDictionary<string, JToken>? additionalProperties)
        {
            // Branch: take this path when (additionalProperties is null || additionalProperties.Count == 0) evaluates to true.
            if (additionalProperties is null || additionalProperties.Count == 0)
            {
                return false;
            }

            foreach (var key in additionalProperties.Keys)
            {
                // Branch: take this path when (string.Equals(key, "expectCanCreate", StringComparison.OrdinalIgnoreCase)) evaluates to true.
                if (string.Equals(key, "expectCanCreate", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
            /// Gets or sets additional properties.
            /// </summary>
            /// <value>The additional properties value.</value>
            [JsonExtensionData]
            public IDictionary<string, JToken>? AdditionalProperties { get; set; }
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
            var requiredMappingExpectations = new Dictionary<string, DuckTypeAotCatalogRequiredMappingExpectation>(StringComparer.Ordinal);

            // Branch: take this path when (string.IsNullOrWhiteSpace(path)) evaluates to true.
            if (string.IsNullOrWhiteSpace(path))
            {
                return new DuckTypeAotMappingCatalogParseResult(Array.Empty<DuckTypeAotCatalogRequiredMappingExpectation>(), errors);
            }

            try
            {
                var json = File.ReadAllText(path);
                var parsedFile = JsonConvert.DeserializeObject<MappingCatalogDocument>(json);
                // Branch: take this path when (parsedFile?.RequiredMappings is null) evaluates to true.
                if (parsedFile?.RequiredMappings is null)
                {
                    errors.Add($"--mapping-catalog content is empty or invalid JSON: {path}");
                    return new DuckTypeAotMappingCatalogParseResult(Array.Empty<DuckTypeAotCatalogRequiredMappingExpectation>(), errors);
                }

                for (var i = 0; i < parsedFile.RequiredMappings.Count; i++)
                {
                    var entry = parsedFile.RequiredMappings[i];
                    // Branch: take this path when (!TryParseCatalogEntry(entry, path, i, errors, out var mapping, out var expectedStatus)) evaluates to true.
                    if (!TryParseCatalogEntry(entry, path, i, errors, out var mapping, out var expectedStatus))
                    {
                        continue;
                    }

                    requiredMappingExpectations[mapping.Key] = new DuckTypeAotCatalogRequiredMappingExpectation(mapping, expectedStatus);
                }
            }
            catch (Exception ex)
            {
                // Branch: handles exceptions that match Exception ex.
                errors.Add($"--mapping-catalog could not be parsed ({path}): {ex.Message}");
            }

            return new DuckTypeAotMappingCatalogParseResult(requiredMappingExpectations.Values, errors);
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
            out DuckTypeAotMapping mapping,
            out string expectedStatus)
        {
            mapping = null!;
            expectedStatus = string.Empty;

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

            // Branch: take this path when (ContainsDeprecatedExpectCanCreate(entry.AdditionalProperties)) evaluates to true.
            if (ContainsDeprecatedExpectCanCreate(entry.AdditionalProperties))
            {
                errors.Add($"--mapping-catalog entry #{index + 1} in '{path}' uses deprecated field 'expectCanCreate'. Strict parity mode requires removing this override field.");
                return false;
            }

            var parsedExpectedStatus = ParseExpectedStatus(entry.ExpectedStatus, path, index, errors);
            // Branch: take this path when (parsedExpectedStatus is null) evaluates to true.
            if (parsedExpectedStatus is null)
            {
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
            expectedStatus = parsedExpectedStatus;
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
        /// Parses parse expected status.
        /// </summary>
        /// <param name="expectedStatus">The expected status value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="index">The index value.</param>
        /// <param name="errors">The errors value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static string? ParseExpectedStatus(string? expectedStatus, string path, int index, ICollection<string> errors)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(expectedStatus)) evaluates to true.
            if (string.IsNullOrWhiteSpace(expectedStatus))
            {
                return DuckTypeAotCompatibilityStatuses.Compatible;
            }

            var normalizedExpectedStatus = expectedStatus!.Trim();
            // Branch: take this path when (TryNormalizeCompatibilityStatus(normalizedExpectedStatus, out var canonicalExpectedStatus)) evaluates to true.
            if (TryNormalizeCompatibilityStatus(normalizedExpectedStatus, out var canonicalExpectedStatus))
            {
                return canonicalExpectedStatus;
            }

            errors.Add(
                $"--mapping-catalog entry #{index + 1} in '{path}' has unsupported expectedStatus '{expectedStatus}'. " +
                "Use one of the known compatibility statuses.");
            return null;
        }

        /// <summary>
        /// Attempts to try normalize compatibility status.
        /// </summary>
        /// <param name="status">The status value.</param>
        /// <param name="normalizedStatus">The normalized status value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryNormalizeCompatibilityStatus(string status, out string normalizedStatus)
        {
            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.Compatible;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.PendingProxyEmission, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.PendingProxyEmission, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.PendingProxyEmission;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.MissingProxyType, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.MissingProxyType, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.MissingProxyType;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.MissingTargetType, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.MissingTargetType, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.MissingTargetType;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.MissingTargetMethod, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.MissingTargetMethod, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.MissingTargetMethod;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.NonPublicTargetMethod, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.NonPublicTargetMethod, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.NonPublicTargetMethod;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.UnsupportedProxyConstructor, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.UnsupportedProxyConstructor, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.UnsupportedProxyConstructor;
                return true;
            }

            // Branch: take this path when (string.Equals(status, DuckTypeAotCompatibilityStatuses.UnsupportedClosedGenericMapping, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(status, DuckTypeAotCompatibilityStatuses.UnsupportedClosedGenericMapping, StringComparison.OrdinalIgnoreCase))
            {
                normalizedStatus = DuckTypeAotCompatibilityStatuses.UnsupportedClosedGenericMapping;
                return true;
            }

            normalizedStatus = string.Empty;
            return false;
        }

        /// <summary>
        /// Determines whether contains deprecated expect can create.
        /// </summary>
        /// <param name="additionalProperties">The additional properties value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ContainsDeprecatedExpectCanCreate(IDictionary<string, JToken>? additionalProperties)
        {
            // Branch: take this path when (additionalProperties is null || additionalProperties.Count == 0) evaluates to true.
            if (additionalProperties is null || additionalProperties.Count == 0)
            {
                return false;
            }

            foreach (var key in additionalProperties.Keys)
            {
                // Branch: take this path when (string.Equals(key, "expectCanCreate", StringComparison.OrdinalIgnoreCase)) evaluates to true.
                if (string.Equals(key, "expectCanCreate", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
            /// Gets or sets expected status.
            /// </summary>
            /// <value>The expected status value.</value>
            [JsonProperty("expectedStatus")]
            public string? ExpectedStatus { get; set; }

            /// <summary>
            /// Gets or sets additional properties.
            /// </summary>
            /// <value>The additional properties value.</value>
            [JsonExtensionData]
            public IDictionary<string, JToken>? AdditionalProperties { get; set; }
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
        /// <param name="requiredMappingExpectations">The required mapping expectations value.</param>
        /// <param name="errors">The errors value.</param>
        public DuckTypeAotMappingCatalogParseResult(
            IEnumerable<DuckTypeAotCatalogRequiredMappingExpectation> requiredMappingExpectations,
            IReadOnlyList<string> errors)
        {
            RequiredMappingExpectations = new List<DuckTypeAotCatalogRequiredMappingExpectation>(requiredMappingExpectations);
            var requiredMappings = new List<DuckTypeAotMapping>(RequiredMappingExpectations.Count);
            foreach (var requiredMappingExpectation in RequiredMappingExpectations)
            {
                requiredMappings.Add(requiredMappingExpectation.Mapping);
            }

            RequiredMappings = requiredMappings;
            Errors = errors;
        }

        /// <summary>
        /// Gets required mapping expectations.
        /// </summary>
        /// <value>The required mapping expectations value.</value>
        public IReadOnlyList<DuckTypeAotCatalogRequiredMappingExpectation> RequiredMappingExpectations { get; }

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

    /// <summary>
    /// Represents duck type aot catalog required mapping expectation.
    /// </summary>
    internal sealed class DuckTypeAotCatalogRequiredMappingExpectation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotCatalogRequiredMappingExpectation"/> class.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="expectedStatus">The expected status value.</param>
        public DuckTypeAotCatalogRequiredMappingExpectation(DuckTypeAotMapping mapping, string expectedStatus)
        {
            Mapping = mapping;
            ExpectedStatus = expectedStatus;
        }

        /// <summary>
        /// Gets mapping.
        /// </summary>
        /// <value>The mapping value.</value>
        public DuckTypeAotMapping Mapping { get; }

        /// <summary>
        /// Gets expected status.
        /// </summary>
        /// <value>The expected status value.</value>
        public string ExpectedStatus { get; }
    }
}
