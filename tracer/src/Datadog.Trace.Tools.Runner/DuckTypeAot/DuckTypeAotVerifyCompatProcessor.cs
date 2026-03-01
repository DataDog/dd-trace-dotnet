// <copyright file="DuckTypeAotVerifyCompatProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Provides helper operations for duck type aot verify compat processor.
    /// </summary>
    internal static class DuckTypeAotVerifyCompatProcessor
    {
        /// <summary>
        /// Executes process.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The computed numeric value.</returns>
        internal static int Process(DuckTypeAotVerifyCompatOptions options)
        {
            // Branch: take this path when (!File.Exists(options.CompatReportPath)) evaluates to true.
            if (!File.Exists(options.CompatReportPath))
            {
                Utils.WriteError($"--compat-report file was not found: {options.CompatReportPath}");
                return 1;
            }

            // Branch: take this path when (!File.Exists(options.CompatMatrixPath)) evaluates to true.
            if (!File.Exists(options.CompatMatrixPath))
            {
                Utils.WriteError($"--compat-matrix file was not found: {options.CompatMatrixPath}");
                return 1;
            }

            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.MappingCatalogPath) && !File.Exists(options.MappingCatalogPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.MappingCatalogPath) && !File.Exists(options.MappingCatalogPath))
            {
                Utils.WriteError($"--mapping-catalog file was not found: {options.MappingCatalogPath}");
                return 1;
            }

            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.ManifestPath) && !File.Exists(options.ManifestPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.ManifestPath) && !File.Exists(options.ManifestPath))
            {
                Utils.WriteError($"--manifest file was not found: {options.ManifestPath}");
                return 1;
            }

            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.ScenarioInventoryPath) && !File.Exists(options.ScenarioInventoryPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.ScenarioInventoryPath) && !File.Exists(options.ScenarioInventoryPath))
            {
                Utils.WriteError($"--scenario-inventory file was not found: {options.ScenarioInventoryPath}");
                return 1;
            }

            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.ExpectedOutcomesPath) && !File.Exists(options.ExpectedOutcomesPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.ExpectedOutcomesPath) && !File.Exists(options.ExpectedOutcomesPath))
            {
                Utils.WriteError($"--expected-outcomes file was not found: {options.ExpectedOutcomesPath}");
                return 1;
            }

            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.KnownLimitationsPath) && !File.Exists(options.KnownLimitationsPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.KnownLimitationsPath) && !File.Exists(options.KnownLimitationsPath))
            {
                Utils.WriteError($"--known-limitations file was not found: {options.KnownLimitationsPath}");
                return 1;
            }

            DuckTypeAotManifest? manifest = null;
            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.ManifestPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.ManifestPath))
            {
                // Branch: take this path when (!TryReadManifest(options.ManifestPath!, out manifest)) evaluates to true.
                if (!TryReadManifest(options.ManifestPath!, out manifest))
                {
                    return 1;
                }
            }

            DuckTypeAotCompatibilityMatrix? matrix;
            try
            {
                matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(options.CompatMatrixPath));
            }
            catch (System.Exception ex)
            {
                // Branch: handles exceptions that match System.Exception ex.
                Utils.WriteError($"--compat-matrix file could not be parsed: {ex.Message}");
                return 1;
            }

            // Branch: take this path when (matrix?.Mappings is null || matrix.Mappings.Count == 0) evaluates to true.
            if (matrix?.Mappings is null || matrix.Mappings.Count == 0)
            {
                Utils.WriteError("--compat-matrix does not contain any mappings.");
                return 1;
            }

            var expectedOutcomes = DuckTypeAotExpectedOutcomes.DefaultCompatible;
            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.ExpectedOutcomesPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.ExpectedOutcomesPath))
            {
                // Branch: take this path when (!TryReadExpectedOutcomes(options.ExpectedOutcomesPath!, out expectedOutcomes)) evaluates to true.
                if (!TryReadExpectedOutcomes(options.ExpectedOutcomesPath!, out expectedOutcomes))
                {
                    return 1;
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.KnownLimitationsPath))
            {
                // Branch: take this path when (!string.IsNullOrWhiteSpace(options.KnownLimitationsPath)) evaluates to true.
                Utils.WriteWarning("--known-limitations is deprecated. Use --expected-outcomes instead.");
                // Branch: take this path when (!TryReadLegacyKnownLimitationsAsExpectedOutcomes(options.KnownLimitationsPath!, out expectedOutcomes)) evaluates to true.
                if (!TryReadLegacyKnownLimitationsAsExpectedOutcomes(options.KnownLimitationsPath!, out expectedOutcomes))
                {
                    return 1;
                }
            }

            // Branch: take this path when (!ValidateExpectedOutcomes(matrix, expectedOutcomes)) evaluates to true.
            if (!ValidateExpectedOutcomes(matrix, expectedOutcomes))
            {
                return 1;
            }

            // Branch: take this path when (manifest is not null) evaluates to true.
            if (manifest is not null)
            {
                // Branch: take this path when (!ValidateManifest(matrix, manifest)) evaluates to true.
                if (!ValidateManifest(matrix, manifest))
                {
                    return 1;
                }

                // Branch: take this path when (!ValidateManifestAssemblyFingerprints(manifest, options.StrictAssemblyFingerprintValidation)) evaluates to true.
                if (!ValidateManifestAssemblyFingerprints(manifest, options.StrictAssemblyFingerprintValidation))
                {
                    return 1;
                }

                // Branch: take this path when (!ValidateManifestGeneratedArtifacts(manifest, options.StrictAssemblyFingerprintValidation)) evaluates to true.
                if (!ValidateManifestGeneratedArtifacts(manifest, options.StrictAssemblyFingerprintValidation))
                {
                    return 1;
                }

                // Branch: take this path when (!ValidateTrimmerDescriptorCoupling(matrix, manifest)) evaluates to true.
                if (!ValidateTrimmerDescriptorCoupling(matrix, manifest))
                {
                    return 1;
                }
            }

            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.MappingCatalogPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.MappingCatalogPath))
            {
                // Branch: take this path when (!ValidateMappingCatalog(matrix, options.MappingCatalogPath!, expectedOutcomes)) evaluates to true.
                if (!ValidateMappingCatalog(matrix, options.MappingCatalogPath!, expectedOutcomes))
                {
                    return 1;
                }
            }

            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.ScenarioInventoryPath)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.ScenarioInventoryPath))
            {
                // Branch: take this path when (!ValidateScenarioInventory(matrix, options.ScenarioInventoryPath!)) evaluates to true.
                if (!ValidateScenarioInventory(matrix, options.ScenarioInventoryPath!))
                {
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Validates validate expected outcomes.
        /// </summary>
        /// <param name="matrix">The matrix value.</param>
        /// <param name="expectedOutcomes">The expected outcomes value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateExpectedOutcomes(
            DuckTypeAotCompatibilityMatrix matrix,
            DuckTypeAotExpectedOutcomes expectedOutcomes)
        {
            var errors = new List<string>();
            var observedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < matrix.Mappings.Count; i++)
            {
                var mapping = matrix.Mappings[i];
                // Branch: take this path when (string.IsNullOrWhiteSpace(mapping.Id)) evaluates to true.
                if (string.IsNullOrWhiteSpace(mapping.Id))
                {
                    errors.Add(
                        $"--compat-matrix mapping entry #{i + 1} is missing scenario id while expected-outcomes validation is enabled. " +
                        $"proxy='{mapping.ProxyType ?? "(null)"}', target='{mapping.TargetType ?? "(null)"}'.");
                    continue;
                }

                var scenarioId = mapping.Id!;
                var actualStatus = mapping.Status ?? string.Empty;
                _ = observedPairs.Add($"{scenarioId}|{actualStatus}");

                _ = expectedOutcomes.TryGetExpectedStatuses(scenarioId, out var expectedStatuses);

                // Branch: take this path when (!expectedStatuses.Contains(actualStatus)) evaluates to true.
                if (!expectedStatuses.Contains(actualStatus))
                {
                    errors.Add(
                        $"Compatibility status mismatch for scenario '{scenarioId}'. " +
                        $"Expected one of [{string.Join(", ", expectedStatuses)}], actual '{actualStatus}'.");
                }
            }

            foreach (var expectedOutcome in expectedOutcomes.ExplicitOutcomes)
            {
                var expectedPair = $"{expectedOutcome.ScenarioId}|{expectedOutcome.Status}";
                // Branch: take this path when (!observedPairs.Contains(expectedPair)) evaluates to true.
                if (!observedPairs.Contains(expectedPair))
                {
                    errors.Add(
                        $"--expected-outcomes entry is stale or mismatched: scenario='{expectedOutcome.ScenarioId}', status='{expectedOutcome.Status}'.");
                }
            }

            // Branch: take this path when (errors.Count == 0) evaluates to true.
            if (errors.Count == 0)
            {
                return true;
            }

            foreach (var error in errors)
            {
                Utils.WriteError(error);
            }

            return false;
        }

        /// <summary>
        /// Attempts to try read expected outcomes.
        /// </summary>
        /// <param name="expectedOutcomesPath">The expected outcomes path value.</param>
        /// <param name="expectedOutcomes">The expected outcomes value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryReadExpectedOutcomes(string expectedOutcomesPath, out DuckTypeAotExpectedOutcomes expectedOutcomes)
        {
            expectedOutcomes = DuckTypeAotExpectedOutcomes.DefaultCompatible;
            DuckTypeAotExpectedOutcomesDocument? expectedOutcomesDocument;
            try
            {
                expectedOutcomesDocument = JsonConvert.DeserializeObject<DuckTypeAotExpectedOutcomesDocument>(File.ReadAllText(expectedOutcomesPath));
            }
            catch (Exception ex)
            {
                // Branch: handles exceptions that match Exception ex.
                Utils.WriteError($"--expected-outcomes file could not be parsed ({expectedOutcomesPath}): {ex.Message}");
                return false;
            }

            // Branch: take this path when (expectedOutcomesDocument is null) evaluates to true.
            if (expectedOutcomesDocument is null)
            {
                Utils.WriteError($"--expected-outcomes file is empty or invalid JSON: {expectedOutcomesPath}");
                return false;
            }

            var defaultStatus = string.IsNullOrWhiteSpace(expectedOutcomesDocument.DefaultStatus)
                                    ? DuckTypeAotCompatibilityStatuses.Compatible
                                    : expectedOutcomesDocument.DefaultStatus!.Trim();
            var entries = expectedOutcomesDocument.ExpectedOutcomes
                          ?? expectedOutcomesDocument.Outcomes
                          ?? expectedOutcomesDocument.Expected
                          ?? new List<DuckTypeAotExpectedOutcomeEntry>();

            return TryBuildExpectedOutcomes(entries, defaultStatus, expectedOutcomesPath, "--expected-outcomes", out expectedOutcomes);
        }

        /// <summary>
        /// Attempts to try read legacy known limitations as expected outcomes.
        /// </summary>
        /// <param name="knownLimitationsPath">The known limitations path value.</param>
        /// <param name="expectedOutcomes">The expected outcomes value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryReadLegacyKnownLimitationsAsExpectedOutcomes(string knownLimitationsPath, out DuckTypeAotExpectedOutcomes expectedOutcomes)
        {
            expectedOutcomes = DuckTypeAotExpectedOutcomes.DefaultCompatible;
            DuckTypeAotKnownLimitationsDocument? knownLimitationsDocument;
            try
            {
                knownLimitationsDocument = JsonConvert.DeserializeObject<DuckTypeAotKnownLimitationsDocument>(File.ReadAllText(knownLimitationsPath));
            }
            catch (Exception ex)
            {
                // Branch: handles exceptions that match Exception ex.
                Utils.WriteError($"--known-limitations file could not be parsed ({knownLimitationsPath}): {ex.Message}");
                return false;
            }

            // Branch: take this path when (knownLimitationsDocument is null) evaluates to true.
            if (knownLimitationsDocument is null)
            {
                Utils.WriteError($"--known-limitations file is empty or invalid JSON: {knownLimitationsPath}");
                return false;
            }

            var entries = knownLimitationsDocument.KnownLimitations
                          ?? knownLimitationsDocument.ApprovedLimitations
                          ?? knownLimitationsDocument.Approved
                          ?? new List<DuckTypeAotExpectedOutcomeEntry>();
            // Branch: take this path when (entries.Count == 0) evaluates to true.
            if (entries.Count == 0)
            {
                Utils.WriteError($"--known-limitations does not contain any entries: {knownLimitationsPath}");
                return false;
            }

            return TryBuildExpectedOutcomes(entries, DuckTypeAotCompatibilityStatuses.Compatible, knownLimitationsPath, "--known-limitations", out expectedOutcomes);
        }

        /// <summary>
        /// Attempts to try build expected outcomes.
        /// </summary>
        /// <param name="entries">The entries value.</param>
        /// <param name="defaultStatus">The default status value.</param>
        /// <param name="sourcePath">The source path value.</param>
        /// <param name="optionName">The option name value.</param>
        /// <param name="expectedOutcomes">The expected outcomes value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static bool TryBuildExpectedOutcomes(
            IReadOnlyList<DuckTypeAotExpectedOutcomeEntry> entries,
            string defaultStatus,
            string sourcePath,
            string optionName,
            out DuckTypeAotExpectedOutcomes expectedOutcomes)
        {
            expectedOutcomes = DuckTypeAotExpectedOutcomes.DefaultCompatible;
            var errors = new List<string>();
            var normalizedEntries = new List<DuckTypeAotExpectedOutcome>(entries.Count);
            var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Branch: take this path when (string.IsNullOrWhiteSpace(defaultStatus)) evaluates to true.
            if (string.IsNullOrWhiteSpace(defaultStatus))
            {
                errors.Add($"{optionName} defaultStatus must be non-empty in '{sourcePath}'.");
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var scenarioId = entry?.ScenarioId?.Trim();
                var status = entry?.Status?.Trim();
                // Branch: take this path when (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(status)) evaluates to true.
                if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(status))
                {
                    errors.Add($"{optionName} entry #{i + 1} in '{sourcePath}' must include non-empty scenarioId and status.");
                    continue;
                }

                var pairKey = $"{scenarioId}|{status}";
                // Branch: take this path when (!seenEntries.Add(pairKey)) evaluates to true.
                if (!seenEntries.Add(pairKey))
                {
                    errors.Add($"{optionName} contains duplicate entry '{pairKey}' in '{sourcePath}'.");
                    continue;
                }

                normalizedEntries.Add(new DuckTypeAotExpectedOutcome(scenarioId!, status!));
            }

            // Branch: take this path when (errors.Count > 0) evaluates to true.
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Utils.WriteError(error);
                }

                return false;
            }

            expectedOutcomes = new DuckTypeAotExpectedOutcomes(defaultStatus.Trim(), normalizedEntries);
            return true;
        }

        /// <summary>
        /// Validates validate mapping catalog.
        /// </summary>
        /// <param name="matrix">The matrix value.</param>
        /// <param name="mappingCatalogPath">The mapping catalog path value.</param>
        /// <param name="expectedOutcomes">The expected outcomes value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateMappingCatalog(
            DuckTypeAotCompatibilityMatrix matrix,
            string mappingCatalogPath,
            DuckTypeAotExpectedOutcomes expectedOutcomes)
        {
            var catalogResult = DuckTypeAotMappingCatalogParser.Parse(mappingCatalogPath);
            // Branch: take this path when (catalogResult.Errors.Count > 0) evaluates to true.
            if (catalogResult.Errors.Count > 0)
            {
                foreach (var error in catalogResult.Errors)
                {
                    Utils.WriteError(error);
                }

                return false;
            }

            var errors = new List<string>();
            var matrixMappingByKey = new Dictionary<string, DuckTypeAotCompatibilityMapping>(StringComparer.Ordinal);
            foreach (var matrixMapping in matrix.Mappings)
            {
                // Branch: take this path when (!TryBuildCompatibilityMappingKey(matrixMapping, out var mappingKey, out var error)) evaluates to true.
                if (!TryBuildCompatibilityMappingKey(matrixMapping, out var mappingKey, out var error))
                {
                    errors.Add(error);
                    continue;
                }

                // Branch: take this path when (!matrixMappingByKey.TryAdd(mappingKey, matrixMapping)) evaluates to true.
                if (!matrixMappingByKey.TryAdd(mappingKey, matrixMapping))
                {
                    errors.Add($"--compat-matrix contains duplicate mappings for key '{mappingKey}'.");
                }
            }

            foreach (var requiredMapping in catalogResult.RequiredMappings)
            {
                // Branch: take this path when (string.IsNullOrWhiteSpace(requiredMapping.ScenarioId)) evaluates to true.
                if (string.IsNullOrWhiteSpace(requiredMapping.ScenarioId))
                {
                    errors.Add(
                        $"--mapping-catalog required mapping is missing scenarioId: " +
                        $"mode={requiredMapping.Mode}, proxy={requiredMapping.ProxyTypeName}, target={requiredMapping.TargetTypeName}.");
                    continue;
                }

                // Branch: take this path when (!matrixMappingByKey.TryGetValue(requiredMapping.Key, out var matrixMapping)) evaluates to true.
                if (!matrixMappingByKey.TryGetValue(requiredMapping.Key, out var matrixMapping))
                {
                    errors.Add(
                        $"--compat-matrix is missing required mapping from --mapping-catalog: " +
                        $"mode={requiredMapping.Mode}, proxy={requiredMapping.ProxyTypeName}, target={requiredMapping.TargetTypeName}.");
                    continue;
                }

                var actualStatus = matrixMapping.Status ?? string.Empty;
                _ = expectedOutcomes.TryGetExpectedStatuses(requiredMapping.ScenarioId!, out var expectedStatuses);

                // Branch: take this path when (!expectedStatuses.Contains(actualStatus)) evaluates to true.
                if (!expectedStatuses.Contains(actualStatus))
                {
                    errors.Add(
                        $"Required mapping status does not match expected outcomes: " +
                        $"key='{requiredMapping.Key}', scenario='{matrixMapping.Id ?? "(null)"}', " +
                        $"expected=[{string.Join(", ", expectedStatuses)}], actual='{actualStatus}'.");
                }

                // Branch: take this path when (!string.IsNullOrWhiteSpace(requiredMapping.ScenarioId) && evaluates to true.
                if (!string.IsNullOrWhiteSpace(requiredMapping.ScenarioId) &&
                    !string.Equals(matrixMapping.Id, requiredMapping.ScenarioId, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Scenario id mismatch for required mapping '{requiredMapping.Key}'. " +
                        $"Expected='{requiredMapping.ScenarioId}', actual='{matrixMapping.Id ?? "(null)"}'.");
                }
            }

            // Branch: take this path when (errors.Count == 0) evaluates to true.
            if (errors.Count == 0)
            {
                return true;
            }

            foreach (var error in errors)
            {
                Utils.WriteError(error);
            }

            return false;
        }

        /// <summary>
        /// Validates validate scenario inventory.
        /// </summary>
        /// <param name="matrix">The matrix value.</param>
        /// <param name="scenarioInventoryPath">The scenario inventory path value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateScenarioInventory(DuckTypeAotCompatibilityMatrix matrix, string scenarioInventoryPath)
        {
            var inventoryResult = DuckTypeAotScenarioInventoryParser.Parse(scenarioInventoryPath);
            // Branch: take this path when (inventoryResult.Errors.Count > 0) evaluates to true.
            if (inventoryResult.Errors.Count > 0)
            {
                foreach (var error in inventoryResult.Errors)
                {
                    Utils.WriteError(error);
                }

                return false;
            }

            var errors = new List<string>();
            var matrixScenarioIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < matrix.Mappings.Count; i++)
            {
                var mapping = matrix.Mappings[i];
                // Branch: take this path when (string.IsNullOrWhiteSpace(mapping.Id)) evaluates to true.
                if (string.IsNullOrWhiteSpace(mapping.Id))
                {
                    errors.Add(
                        $"--compat-matrix mapping entry #{i + 1} is missing scenario id while --scenario-inventory is enabled. " +
                        $"proxy='{mapping.ProxyType ?? "(null)"}', target='{mapping.TargetType ?? "(null)"}'.");
                    continue;
                }

                _ = matrixScenarioIds.Add(mapping.Id!);
            }

            foreach (var requiredEntry in inventoryResult.RequiredScenarios)
            {
                // Branch: take this path when (!IsScenarioCoveredByMatrix(requiredEntry, matrixScenarioIds)) evaluates to true.
                if (!IsScenarioCoveredByMatrix(requiredEntry, matrixScenarioIds))
                {
                    errors.Add($"--compat-matrix is missing required scenario from --scenario-inventory: '{requiredEntry}'.");
                }
            }

            foreach (var matrixScenarioId in matrixScenarioIds)
            {
                // Branch: take this path when (!IsScenarioTrackedByInventory(matrixScenarioId, inventoryResult.RequiredScenarios)) evaluates to true.
                if (!IsScenarioTrackedByInventory(matrixScenarioId, inventoryResult.RequiredScenarios))
                {
                    errors.Add(
                        $"--compat-matrix contains scenario id '{matrixScenarioId}' that is not tracked by --scenario-inventory. " +
                        "Add it to the inventory (or matching wildcard group) to avoid unreviewed scenario drift.");
                }
            }

            // Branch: take this path when (errors.Count == 0) evaluates to true.
            if (errors.Count == 0)
            {
                return true;
            }

            foreach (var error in errors)
            {
                Utils.WriteError(error);
            }

            return false;
        }

        /// <summary>
        /// Determines whether is scenario covered by matrix.
        /// </summary>
        /// <param name="requiredEntry">The required entry value.</param>
        /// <param name="matrixScenarioIds">The matrix scenario ids value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsScenarioCoveredByMatrix(string requiredEntry, ISet<string> matrixScenarioIds)
        {
            // Branch: take this path when (!IsWildcardScenarioEntry(requiredEntry)) evaluates to true.
            if (!IsWildcardScenarioEntry(requiredEntry))
            {
                return matrixScenarioIds.Contains(requiredEntry);
            }

            var wildcardPrefix = requiredEntry.Substring(0, requiredEntry.Length - 1);
            foreach (var matrixScenarioId in matrixScenarioIds)
            {
                // Branch: take this path when (matrixScenarioId.StartsWith(wildcardPrefix, StringComparison.Ordinal)) evaluates to true.
                if (matrixScenarioId.StartsWith(wildcardPrefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether is scenario tracked by inventory.
        /// </summary>
        /// <param name="matrixScenarioId">The matrix scenario id value.</param>
        /// <param name="requiredScenarios">The required scenarios value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsScenarioTrackedByInventory(string matrixScenarioId, IReadOnlyList<string> requiredScenarios)
        {
            foreach (var requiredEntry in requiredScenarios)
            {
                // Branch: take this path when (!IsWildcardScenarioEntry(requiredEntry)) evaluates to true.
                if (!IsWildcardScenarioEntry(requiredEntry))
                {
                    // Branch: take this path when (string.Equals(matrixScenarioId, requiredEntry, StringComparison.Ordinal)) evaluates to true.
                    if (string.Equals(matrixScenarioId, requiredEntry, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    continue;
                }

                var wildcardPrefix = requiredEntry.Substring(0, requiredEntry.Length - 1);
                // Branch: take this path when (matrixScenarioId.StartsWith(wildcardPrefix, StringComparison.Ordinal)) evaluates to true.
                if (matrixScenarioId.StartsWith(wildcardPrefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether is wildcard scenario entry.
        /// </summary>
        /// <param name="entry">The entry value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static bool IsWildcardScenarioEntry(string entry)
        {
            return entry.Length > 1 && entry[entry.Length - 1] == '*';
        }

        /// <summary>
        /// Attempts to try read manifest.
        /// </summary>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="manifest">The manifest value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryReadManifest(string manifestPath, out DuckTypeAotManifest? manifest)
        {
            try
            {
                manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                // Branch: handles exceptions that match Exception ex.
                Utils.WriteError($"--manifest file could not be parsed: {ex.Message}");
                manifest = null;
                return false;
            }

            // Branch: take this path when (manifest?.Mappings is null || manifest.Mappings.Count == 0) evaluates to true.
            if (manifest?.Mappings is null || manifest.Mappings.Count == 0)
            {
                Utils.WriteError("--manifest does not contain any mappings.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates validate manifest.
        /// </summary>
        /// <param name="matrix">The matrix value.</param>
        /// <param name="manifest">The manifest value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateManifest(DuckTypeAotCompatibilityMatrix matrix, DuckTypeAotManifest manifest)
        {
            var errors = new List<string>();

            // Branch: take this path when (!string.IsNullOrWhiteSpace(matrix.SchemaVersion) && evaluates to true.
            if (!string.IsNullOrWhiteSpace(matrix.SchemaVersion) &&
                !string.IsNullOrWhiteSpace(manifest.SchemaVersion) &&
                !string.Equals(matrix.SchemaVersion, manifest.SchemaVersion, StringComparison.Ordinal))
            {
                errors.Add($"Schema version mismatch between --compat-matrix and --manifest. Matrix='{matrix.SchemaVersion}', manifest='{manifest.SchemaVersion}'.");
            }

            var matrixMappingByKey = new Dictionary<string, DuckTypeAotCompatibilityMapping>(StringComparer.Ordinal);
            foreach (var matrixMapping in matrix.Mappings)
            {
                // Branch: take this path when (!TryBuildCompatibilityMappingKey(matrixMapping, out var mappingKey, out var error)) evaluates to true.
                if (!TryBuildCompatibilityMappingKey(matrixMapping, out var mappingKey, out var error))
                {
                    errors.Add(error);
                    continue;
                }

                // Branch: take this path when (!matrixMappingByKey.TryAdd(mappingKey, matrixMapping)) evaluates to true.
                if (!matrixMappingByKey.TryAdd(mappingKey, matrixMapping))
                {
                    errors.Add($"--compat-matrix contains duplicate mappings for key '{mappingKey}'.");
                }
            }

            var manifestMappingByKey = new Dictionary<string, DuckTypeAotManifestMapping>(StringComparer.Ordinal);
            foreach (var manifestMapping in manifest.Mappings)
            {
                // Branch: take this path when (!TryBuildManifestMappingKey(manifestMapping, out var mappingKey, out var error)) evaluates to true.
                if (!TryBuildManifestMappingKey(manifestMapping, out var mappingKey, out var error))
                {
                    errors.Add(error);
                    continue;
                }

                // Branch: take this path when (!manifestMappingByKey.TryAdd(mappingKey, manifestMapping)) evaluates to true.
                if (!manifestMappingByKey.TryAdd(mappingKey, manifestMapping))
                {
                    errors.Add($"--manifest contains duplicate mappings for key '{mappingKey}'.");
                }
            }

            foreach (var (mappingKey, matrixMapping) in matrixMappingByKey)
            {
                // Branch: take this path when (!manifestMappingByKey.TryGetValue(mappingKey, out var manifestMapping)) evaluates to true.
                if (!manifestMappingByKey.TryGetValue(mappingKey, out var manifestMapping))
                {
                    errors.Add($"--manifest is missing mapping from --compat-matrix: key='{mappingKey}'.");
                    continue;
                }

                // Branch: take this path when (!ValidateChecksum(matrixMapping.MappingIdentityChecksum, out var matrixChecksumError)) evaluates to true.
                if (!ValidateChecksum(matrixMapping.MappingIdentityChecksum, out var matrixChecksumError))
                {
                    errors.Add($"--compat-matrix mapping id '{matrixMapping.Id ?? "(null)"}' has invalid mappingIdentityChecksum: {matrixChecksumError}");
                    continue;
                }

                // Branch: take this path when (!ValidateChecksum(manifestMapping.MappingIdentityChecksum, out var manifestChecksumError)) evaluates to true.
                if (!ValidateChecksum(manifestMapping.MappingIdentityChecksum, out var manifestChecksumError))
                {
                    errors.Add($"--manifest mapping key '{mappingKey}' has invalid mappingIdentityChecksum: {manifestChecksumError}");
                    continue;
                }

                // Branch: take this path when (!string.Equals(matrixMapping.MappingIdentityChecksum, manifestMapping.MappingIdentityChecksum, StringComparison.OrdinalIgnoreCase)) evaluates to true.
                if (!string.Equals(matrixMapping.MappingIdentityChecksum, manifestMapping.MappingIdentityChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"mappingIdentityChecksum mismatch for mapping '{mappingKey}'. " +
                        $"Matrix='{matrixMapping.MappingIdentityChecksum}', manifest='{manifestMapping.MappingIdentityChecksum}'.");
                }

                // Branch: take this path when (!string.IsNullOrWhiteSpace(manifestMapping.ScenarioId) && evaluates to true.
                if (!string.IsNullOrWhiteSpace(manifestMapping.ScenarioId) &&
                    !string.Equals(matrixMapping.Id, manifestMapping.ScenarioId, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Scenario id mismatch between --compat-matrix and --manifest for mapping '{mappingKey}'. " +
                        $"Matrix='{matrixMapping.Id ?? "(null)"}', manifest='{manifestMapping.ScenarioId}'.");
                }
            }

            foreach (var mappingKey in manifestMappingByKey.Keys)
            {
                // Branch: take this path when (!matrixMappingByKey.ContainsKey(mappingKey)) evaluates to true.
                if (!matrixMappingByKey.ContainsKey(mappingKey))
                {
                    errors.Add($"--compat-matrix is missing mapping from --manifest: key='{mappingKey}'.");
                }
            }

            // Branch: take this path when (errors.Count == 0) evaluates to true.
            if (errors.Count == 0)
            {
                return true;
            }

            foreach (var error in errors)
            {
                Utils.WriteError(error);
            }

            return false;
        }

        /// <summary>
        /// Validates validate manifest assembly fingerprints.
        /// </summary>
        /// <param name="manifest">The manifest value.</param>
        /// <param name="strictAssemblyFingerprintValidation">The strict assembly fingerprint validation value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateManifestAssemblyFingerprints(DuckTypeAotManifest manifest, bool strictAssemblyFingerprintValidation)
        {
            var issues = new List<string>();

            ValidateFingerprints(manifest.TargetAssemblies, "target", issues);
            ValidateFingerprints(manifest.ProxyAssemblies, "proxy", issues);

            // Branch: take this path when (issues.Count == 0) evaluates to true.
            if (issues.Count == 0)
            {
                return true;
            }

            foreach (var issue in issues)
            {
                // Branch: take this path when (strictAssemblyFingerprintValidation) evaluates to true.
                if (strictAssemblyFingerprintValidation)
                {
                    Utils.WriteError(issue);
                }
                else
                {
                    // Branch: fallback path when earlier branch conditions evaluate to false.
                    Utils.WriteWarning(issue);
                }
            }

            return !strictAssemblyFingerprintValidation;
        }

        /// <summary>
        /// Validates validate manifest generated artifacts.
        /// </summary>
        /// <param name="manifest">The manifest value.</param>
        /// <param name="strictAssemblyFingerprintValidation">The strict assembly fingerprint validation value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateManifestGeneratedArtifacts(DuckTypeAotManifest manifest, bool strictAssemblyFingerprintValidation)
        {
            var issues = new List<string>();

            ValidateFileFingerprint(
                manifest.RegistryAssembly,
                manifest.RegistryAssemblySha256,
                "registry assembly",
                issues);
            ValidateFileFingerprint(
                manifest.TrimmerDescriptorPath,
                manifest.TrimmerDescriptorSha256,
                "trimmer descriptor",
                issues);
            ValidateFileFingerprint(
                manifest.PropsPath,
                manifest.PropsSha256,
                "props file",
                issues);

            // Branch: take this path when (!string.IsNullOrWhiteSpace(manifest.RegistryAssembly) && File.Exists(manifest.RegistryAssembly)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(manifest.RegistryAssembly) && File.Exists(manifest.RegistryAssembly))
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(manifest.RegistryAssembly);
                    var actualVersion = assemblyName.Version?.ToString() ?? "0.0.0.0";
                    var actualPublicKeyTokenBytes = assemblyName.GetPublicKeyToken();
                    var actualPublicKeyToken = actualPublicKeyTokenBytes is { Length: > 0 }
                                                   ? BitConverter.ToString(actualPublicKeyTokenBytes).Replace("-", string.Empty).ToLowerInvariant()
                                                   : string.Empty;
                    var actualIsStrongNameSigned = !string.IsNullOrWhiteSpace(actualPublicKeyToken);

                    // Branch: take this path when (!string.IsNullOrWhiteSpace(manifest.RegistryAssemblyVersion) && evaluates to true.
                    if (!string.IsNullOrWhiteSpace(manifest.RegistryAssemblyVersion) &&
                        !string.Equals(actualVersion, manifest.RegistryAssemblyVersion, StringComparison.Ordinal))
                    {
                        issues.Add($"Manifest registry assembly version mismatch. Expected '{manifest.RegistryAssemblyVersion}', got '{actualVersion}'.");
                    }

                    // Branch: take this path when (manifest.RegistryStrongNameSigned.HasValue && evaluates to true.
                    if (manifest.RegistryStrongNameSigned.HasValue &&
                        manifest.RegistryStrongNameSigned.Value != actualIsStrongNameSigned)
                    {
                        issues.Add(
                            $"Manifest registry strong-name flag mismatch. " +
                            $"Expected '{manifest.RegistryStrongNameSigned.Value}', got '{actualIsStrongNameSigned}'.");
                    }

                    // Branch: take this path when (!string.IsNullOrWhiteSpace(manifest.RegistryPublicKeyToken)) evaluates to true.
                    if (!string.IsNullOrWhiteSpace(manifest.RegistryPublicKeyToken))
                    {
                        // Branch: take this path when (!actualIsStrongNameSigned) evaluates to true.
                        if (!actualIsStrongNameSigned)
                        {
                            issues.Add(
                                $"Manifest registry public key token is set ('{manifest.RegistryPublicKeyToken}') but registry assembly is not strong-name signed.");
                        }
                        else if (!string.Equals(actualPublicKeyToken, manifest.RegistryPublicKeyToken, StringComparison.OrdinalIgnoreCase))
                        {
                            // Branch: take this path when (!string.Equals(actualPublicKeyToken, manifest.RegistryPublicKeyToken, StringComparison.OrdinalIgnoreCase)) evaluates to true.
                            issues.Add(
                                $"Manifest registry public key token mismatch. Expected '{manifest.RegistryPublicKeyToken}', got '{actualPublicKeyToken}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Branch: handles exceptions that match Exception ex.
                    issues.Add($"Manifest registry assembly metadata could not be validated: {ex.Message}");
                }
            }

            // Branch: take this path when (issues.Count == 0) evaluates to true.
            if (issues.Count == 0)
            {
                return true;
            }

            foreach (var issue in issues)
            {
                // Branch: take this path when (strictAssemblyFingerprintValidation) evaluates to true.
                if (strictAssemblyFingerprintValidation)
                {
                    Utils.WriteError(issue);
                }
                else
                {
                    // Branch: fallback path when earlier branch conditions evaluate to false.
                    Utils.WriteWarning(issue);
                }
            }

            return !strictAssemblyFingerprintValidation;
        }

        /// <summary>
        /// Validates validate trimmer descriptor coupling.
        /// </summary>
        /// <param name="matrix">The matrix value.</param>
        /// <param name="manifest">The manifest value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateTrimmerDescriptorCoupling(DuckTypeAotCompatibilityMatrix matrix, DuckTypeAotManifest manifest)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(manifest.TrimmerDescriptorPath)) evaluates to true.
            if (string.IsNullOrWhiteSpace(manifest.TrimmerDescriptorPath))
            {
                return true;
            }

            // Branch: take this path when (!TryReadTrimmerDescriptorRoots(manifest.TrimmerDescriptorPath!, out var rootsByAssembly, out var readError)) evaluates to true.
            if (!TryReadTrimmerDescriptorRoots(manifest.TrimmerDescriptorPath!, out var rootsByAssembly, out var readError))
            {
                Utils.WriteError(readError);
                return false;
            }

            var errors = new List<string>();

            // Branch: take this path when (!string.IsNullOrWhiteSpace(manifest.RegistryAssemblyName) && evaluates to true.
            if (!string.IsNullOrWhiteSpace(manifest.RegistryAssemblyName) &&
                !string.IsNullOrWhiteSpace(manifest.RegistryBootstrapType))
            {
                ValidateTrimmerDescriptorRoot(
                    rootsByAssembly,
                    manifest.RegistryAssemblyName!,
                    manifest.RegistryBootstrapType!,
                    "registry bootstrap type",
                    errors);
            }

            foreach (var mapping in matrix.Mappings)
            {
                // Branch: take this path when (!string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase)) evaluates to true.
                if (!string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Branch: take this path when (!string.IsNullOrWhiteSpace(mapping.ProxyAssembly) && !string.IsNullOrWhiteSpace(mapping.ProxyType)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(mapping.ProxyAssembly) && !string.IsNullOrWhiteSpace(mapping.ProxyType))
                {
                    ValidateTrimmerDescriptorRoot(
                        rootsByAssembly,
                        mapping.ProxyAssembly!,
                        mapping.ProxyType!,
                        $"compatible mapping '{mapping.Id ?? "(null)"}' proxy root",
                        errors);
                }

                // Branch: take this path when (!string.IsNullOrWhiteSpace(mapping.TargetAssembly) && !string.IsNullOrWhiteSpace(mapping.TargetType)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(mapping.TargetAssembly) && !string.IsNullOrWhiteSpace(mapping.TargetType))
                {
                    ValidateTrimmerDescriptorRoot(
                        rootsByAssembly,
                        mapping.TargetAssembly!,
                        mapping.TargetType!,
                        $"compatible mapping '{mapping.Id ?? "(null)"}' target root",
                        errors);
                }

                // Branch: take this path when (!string.IsNullOrWhiteSpace(mapping.GeneratedProxyAssembly) && !string.IsNullOrWhiteSpace(mapping.GeneratedProxyType)) evaluates to true.
                if (!string.IsNullOrWhiteSpace(mapping.GeneratedProxyAssembly) && !string.IsNullOrWhiteSpace(mapping.GeneratedProxyType))
                {
                    ValidateTrimmerDescriptorRoot(
                        rootsByAssembly,
                        mapping.GeneratedProxyAssembly!,
                        mapping.GeneratedProxyType!,
                        $"compatible mapping '{mapping.Id ?? "(null)"}' generated proxy root",
                        errors);
                }
            }

            // Branch: take this path when (manifest.GenericInstantiations is not null) evaluates to true.
            if (manifest.GenericInstantiations is not null)
            {
                foreach (var typeReference in manifest.GenericInstantiations)
                {
                    // Branch: take this path when (string.IsNullOrWhiteSpace(typeReference.Assembly) || string.IsNullOrWhiteSpace(typeReference.Type)) evaluates to true.
                    if (string.IsNullOrWhiteSpace(typeReference.Assembly) || string.IsNullOrWhiteSpace(typeReference.Type))
                    {
                        continue;
                    }

                    ValidateTrimmerDescriptorRoot(
                        rootsByAssembly,
                        typeReference.Assembly!,
                        typeReference.Type!,
                        $"generic root '{typeReference.Type}'",
                        errors);
                }
            }

            // Branch: take this path when (errors.Count == 0) evaluates to true.
            if (errors.Count == 0)
            {
                return true;
            }

            foreach (var error in errors)
            {
                Utils.WriteError(error);
            }

            return false;
        }

        /// <summary>
        /// Attempts to try read trimmer descriptor roots.
        /// </summary>
        /// <param name="descriptorPath">The descriptor path value.</param>
        /// <param name="rootsByAssembly">The roots by assembly value.</param>
        /// <param name="error">The error value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryReadTrimmerDescriptorRoots(
            string descriptorPath,
            out Dictionary<string, HashSet<string>> rootsByAssembly,
            out string error)
        {
            rootsByAssembly = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            // Branch: take this path when (!File.Exists(descriptorPath)) evaluates to true.
            if (!File.Exists(descriptorPath))
            {
                error = $"Manifest trimmer descriptor path was not found: {descriptorPath}";
                return false;
            }

            try
            {
                var document = XDocument.Load(descriptorPath);
                var linker = document.Root;
                // Branch: take this path when (linker is null || !string.Equals(linker.Name.LocalName, "linker", StringComparison.Ordinal)) evaluates to true.
                if (linker is null || !string.Equals(linker.Name.LocalName, "linker", StringComparison.Ordinal))
                {
                    error = $"Trimmer descriptor is invalid (missing <linker> root): {descriptorPath}";
                    return false;
                }

                foreach (var assemblyElement in linker.Elements())
                {
                    // Branch: take this path when (!string.Equals(assemblyElement.Name.LocalName, "assembly", StringComparison.Ordinal)) evaluates to true.
                    if (!string.Equals(assemblyElement.Name.LocalName, "assembly", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var assemblyName = assemblyElement.Attribute("fullname")?.Value;
                    // Branch: take this path when (string.IsNullOrWhiteSpace(assemblyName)) evaluates to true.
                    if (string.IsNullOrWhiteSpace(assemblyName))
                    {
                        continue;
                    }

                    // Branch: take this path when (!rootsByAssembly.TryGetValue(assemblyName!, out var typeRoots)) evaluates to true.
                    if (!rootsByAssembly.TryGetValue(assemblyName!, out var typeRoots))
                    {
                        typeRoots = new HashSet<string>(StringComparer.Ordinal);
                        rootsByAssembly[assemblyName!] = typeRoots;
                    }

                    foreach (var typeElement in assemblyElement.Elements())
                    {
                        // Branch: take this path when (!string.Equals(typeElement.Name.LocalName, "type", StringComparison.Ordinal)) evaluates to true.
                        if (!string.Equals(typeElement.Name.LocalName, "type", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var typeName = typeElement.Attribute("fullname")?.Value;
                        // Branch: take this path when (string.IsNullOrWhiteSpace(typeName)) evaluates to true.
                        if (string.IsNullOrWhiteSpace(typeName))
                        {
                            continue;
                        }

                        _ = typeRoots.Add(typeName!);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // Branch: handles exceptions that match Exception ex.
                error = $"Failed to parse trimmer descriptor '{descriptorPath}': {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Validates validate trimmer descriptor root.
        /// </summary>
        /// <param name="rootsByAssembly">The roots by assembly value.</param>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <param name="typeName">The type name value.</param>
        /// <param name="context">The context value.</param>
        /// <param name="errors">The errors value.</param>
        private static void ValidateTrimmerDescriptorRoot(
            IReadOnlyDictionary<string, HashSet<string>> rootsByAssembly,
            string assemblyName,
            string typeName,
            string context,
            ICollection<string> errors)
        {
            // Branch: take this path when (!rootsByAssembly.TryGetValue(assemblyName, out var typeRoots)) evaluates to true.
            if (!rootsByAssembly.TryGetValue(assemblyName, out var typeRoots))
            {
                errors.Add($"Trimmer descriptor is missing assembly root '{assemblyName}' required for {context}.");
                return;
            }

            var normalizedTypeName = NormalizeTypeNameForLinker(typeName);
            // Branch: take this path when (!typeRoots.Contains(normalizedTypeName)) evaluates to true.
            if (!typeRoots.Contains(normalizedTypeName))
            {
                errors.Add($"Trimmer descriptor is missing type root '{normalizedTypeName}' in assembly '{assemblyName}' required for {context}.");
            }
        }

        /// <summary>
        /// Validates validate file fingerprint.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <param name="expectedSha256">The expected sha256 value.</param>
        /// <param name="artifactName">The artifact name value.</param>
        /// <param name="issues">The issues value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void ValidateFileFingerprint(string? path, string? expectedSha256, string artifactName, ICollection<string> issues)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(path)) evaluates to true.
            if (string.IsNullOrWhiteSpace(path))
            {
                issues.Add($"Manifest {artifactName} path is missing.");
                return;
            }

            var resolvedPath = path!;
            // Branch: take this path when (!File.Exists(resolvedPath)) evaluates to true.
            if (!File.Exists(resolvedPath))
            {
                issues.Add($"Manifest {artifactName} path was not found: {resolvedPath}");
                return;
            }

            // Branch: take this path when (!ValidateChecksum(expectedSha256, out var checksumError)) evaluates to true.
            if (!ValidateChecksum(expectedSha256, out var checksumError))
            {
                issues.Add($"Manifest {artifactName} has invalid sha256 for '{resolvedPath}': {checksumError}.");
                return;
            }

            var actualSha256 = ComputeSha256(resolvedPath);
            // Branch: take this path when (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Manifest {artifactName} sha256 mismatch for '{resolvedPath}'. Expected '{expectedSha256}', got '{actualSha256}'.");
            }
        }

        /// <summary>
        /// Attempts to try build compatibility mapping key.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="key">The key value.</param>
        /// <param name="error">The error value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static bool TryBuildCompatibilityMappingKey(
            DuckTypeAotCompatibilityMapping mapping,
            out string key,
            out string error)
        {
            key = string.Empty;
            error = string.Empty;

            // Branch: take this path when (!TryParseMode(mapping.Mode, out var mode)) evaluates to true.
            if (!TryParseMode(mapping.Mode, out var mode))
            {
                error = $"--compat-matrix mapping id '{mapping.Id ?? "(null)"}' has invalid mode '{mapping.Mode ?? "(null)"}'.";
                return false;
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(mapping.ProxyType) || evaluates to true.
            if (string.IsNullOrWhiteSpace(mapping.ProxyType) ||
                string.IsNullOrWhiteSpace(mapping.ProxyAssembly) ||
                string.IsNullOrWhiteSpace(mapping.TargetType) ||
                string.IsNullOrWhiteSpace(mapping.TargetAssembly))
            {
                error = $"--compat-matrix mapping id '{mapping.Id ?? "(null)"}' is missing proxy/target type or assembly values.";
                return false;
            }

            var proxyType = mapping.ProxyType!;
            var proxyAssembly = mapping.ProxyAssembly!;
            var targetType = mapping.TargetType!;
            var targetAssembly = mapping.TargetAssembly!;
            key = new DuckTypeAotMapping(
                    proxyType,
                    proxyAssembly,
                    targetType,
                    targetAssembly,
                    mode,
                    DuckTypeAotMappingSource.MapFile)
                .Key;
            return true;
        }

        /// <summary>
        /// Attempts to try build manifest mapping key.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="key">The key value.</param>
        /// <param name="error">The error value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static bool TryBuildManifestMappingKey(
            DuckTypeAotManifestMapping mapping,
            out string key,
            out string error)
        {
            key = string.Empty;
            error = string.Empty;

            // Branch: take this path when (!TryParseMode(mapping.Mode, out var mode)) evaluates to true.
            if (!TryParseMode(mapping.Mode, out var mode))
            {
                error = $"--manifest mapping has invalid mode '{mapping.Mode ?? "(null)"}'.";
                return false;
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(mapping.ProxyType) || evaluates to true.
            if (string.IsNullOrWhiteSpace(mapping.ProxyType) ||
                string.IsNullOrWhiteSpace(mapping.ProxyAssembly) ||
                string.IsNullOrWhiteSpace(mapping.TargetType) ||
                string.IsNullOrWhiteSpace(mapping.TargetAssembly))
            {
                error = "--manifest mapping is missing proxy/target type or assembly values.";
                return false;
            }

            var proxyType = mapping.ProxyType!;
            var proxyAssembly = mapping.ProxyAssembly!;
            var targetType = mapping.TargetType!;
            var targetAssembly = mapping.TargetAssembly!;
            key = new DuckTypeAotMapping(
                    proxyType,
                    proxyAssembly,
                    targetType,
                    targetAssembly,
                    mode,
                    DuckTypeAotMappingSource.MapFile)
                .Key;
            return true;
        }

        /// <summary>
        /// Validates validate checksum.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="error">The error value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateChecksum(string? value, out string error)
        {
            // Branch: take this path when (string.IsNullOrWhiteSpace(value)) evaluates to true.
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "value is empty";
                return false;
            }

            var checksum = value!;
            // Branch: take this path when (checksum.Length != 64) evaluates to true.
            if (checksum.Length != 64)
            {
                error = $"value must be 64 hex chars, got length {checksum.Length}";
                return false;
            }

            for (var i = 0; i < checksum.Length; i++)
            {
                var c = checksum[i];
                // Branch: take this path when ((c >= '0' && c <= '9') || evaluates to true.
                if ((c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F'))
                {
                    continue;
                }

                error = $"value contains non-hex character '{c}' at position {i}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates validate fingerprints.
        /// </summary>
        /// <param name="fingerprints">The fingerprints value.</param>
        /// <param name="assemblyKind">The assembly kind value.</param>
        /// <param name="issues">The issues value.</param>
        private static void ValidateFingerprints(
            IReadOnlyList<DuckTypeAotAssemblyFingerprint>? fingerprints,
            string assemblyKind,
            ICollection<string> issues)
        {
            // Branch: take this path when (fingerprints is null || fingerprints.Count == 0) evaluates to true.
            if (fingerprints is null || fingerprints.Count == 0)
            {
                return;
            }

            foreach (var fingerprint in fingerprints)
            {
                var expectedName = fingerprint.Name ?? "(unknown)";
                var assemblyPath = fingerprint.Path;
                // Branch: take this path when (string.IsNullOrWhiteSpace(assemblyPath)) evaluates to true.
                if (string.IsNullOrWhiteSpace(assemblyPath))
                {
                    issues.Add($"Manifest {assemblyKind} assembly '{expectedName}' is missing path.");
                    continue;
                }

                var resolvedAssemblyPath = assemblyPath!;
                // Branch: take this path when (!File.Exists(resolvedAssemblyPath)) evaluates to true.
                if (!File.Exists(resolvedAssemblyPath))
                {
                    issues.Add($"Manifest {assemblyKind} assembly '{expectedName}' path was not found: {resolvedAssemblyPath}");
                    continue;
                }

                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(resolvedAssemblyPath);
                    var actualName = assemblyName.Name ?? string.Empty;
                    // Branch: take this path when (!string.IsNullOrWhiteSpace(fingerprint.Name) && evaluates to true.
                    if (!string.IsNullOrWhiteSpace(fingerprint.Name) &&
                        !string.Equals(actualName, fingerprint.Name, StringComparison.Ordinal))
                    {
                        issues.Add($"Manifest {assemblyKind} assembly name mismatch for '{resolvedAssemblyPath}'. Expected '{fingerprint.Name}', got '{actualName}'.");
                    }

                    using var module = ModuleDefMD.Load(resolvedAssemblyPath);
                    var actualMvid = module.Mvid?.ToString("D") ?? string.Empty;
                    // Branch: take this path when (!string.IsNullOrWhiteSpace(fingerprint.Mvid) && evaluates to true.
                    if (!string.IsNullOrWhiteSpace(fingerprint.Mvid) &&
                        !string.Equals(actualMvid, fingerprint.Mvid, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add($"Manifest {assemblyKind} assembly MVID mismatch for '{resolvedAssemblyPath}'. Expected '{fingerprint.Mvid}', got '{actualMvid}'.");
                    }

                    // Branch: take this path when (!ValidateChecksum(fingerprint.Sha256, out var checksumError)) evaluates to true.
                    if (!ValidateChecksum(fingerprint.Sha256, out var checksumError))
                    {
                        issues.Add($"Manifest {assemblyKind} assembly has invalid sha256 for '{resolvedAssemblyPath}': {checksumError}.");
                    }
                    else
                    {
                        // Branch: fallback path when earlier branch conditions evaluate to false.
                        var actualSha256 = ComputeSha256(resolvedAssemblyPath);
                        // Branch: take this path when (!string.Equals(actualSha256, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase)) evaluates to true.
                        if (!string.Equals(actualSha256, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add($"Manifest {assemblyKind} assembly sha256 mismatch for '{resolvedAssemblyPath}'. Expected '{fingerprint.Sha256}', got '{actualSha256}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Branch: handles exceptions that match Exception ex.
                    issues.Add($"Manifest {assemblyKind} assembly '{resolvedAssemblyPath}' could not be validated: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Computes compute sha256.
        /// </summary>
        /// <param name="filePath">The file path value.</param>
        /// <returns>The resulting string value.</returns>
        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var hashByte in hash)
            {
                _ = sb.Append(hashByte.ToString("x2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Normalizes normalize type name for linker.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>The resulting string value.</returns>
        private static string NormalizeTypeNameForLinker(string typeName)
        {
            return typeName.Replace('+', '/');
        }

        /// <summary>
        /// Attempts to try parse mode.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="mode">The mode value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryParseMode(string? value, out DuckTypeAotMappingMode mode)
        {
            // Branch: take this path when (string.Equals(value, "forward", StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(value, "forward", StringComparison.OrdinalIgnoreCase))
            {
                mode = DuckTypeAotMappingMode.Forward;
                return true;
            }

            // Branch: take this path when (string.Equals(value, "reverse", StringComparison.OrdinalIgnoreCase)) evaluates to true.
            if (string.Equals(value, "reverse", StringComparison.OrdinalIgnoreCase))
            {
                mode = DuckTypeAotMappingMode.Reverse;
                return true;
            }

            mode = DuckTypeAotMappingMode.Forward;
            return false;
        }

        /// <summary>
        /// Represents duck type aot expected outcome.
        /// </summary>
        private readonly struct DuckTypeAotExpectedOutcome
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="DuckTypeAotExpectedOutcome"/> struct.
            /// </summary>
            /// <param name="scenarioId">The scenario id value.</param>
            /// <param name="status">The status value.</param>
            internal DuckTypeAotExpectedOutcome(string scenarioId, string status)
            {
                ScenarioId = scenarioId;
                Status = status;
            }

            /// <summary>
            /// Gets scenario id.
            /// </summary>
            /// <value>The scenario id value.</value>
            internal string ScenarioId { get; }

            /// <summary>
            /// Gets status.
            /// </summary>
            /// <value>The status value.</value>
            internal string Status { get; }
        }

        /// <summary>
        /// Represents duck type aot expected outcomes.
        /// </summary>
        private sealed class DuckTypeAotExpectedOutcomes
        {
            /// <summary>
            /// Stores expected statuses by scenario.
            /// </summary>
            private readonly Dictionary<string, HashSet<string>> _expectedStatusesByScenario;

            /// <summary>
            /// Initializes a new instance of the <see cref="DuckTypeAotExpectedOutcomes"/> class.
            /// </summary>
            /// <param name="defaultStatus">The default status value.</param>
            /// <param name="explicitOutcomes">The explicit outcomes value.</param>
            internal DuckTypeAotExpectedOutcomes(string defaultStatus, IReadOnlyList<DuckTypeAotExpectedOutcome> explicitOutcomes)
            {
                DefaultStatus = defaultStatus;
                ExplicitOutcomes = explicitOutcomes;
                _expectedStatusesByScenario = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                foreach (var expectedOutcome in explicitOutcomes)
                {
                    // Branch: take this path when (!_expectedStatusesByScenario.TryGetValue(expectedOutcome.ScenarioId, out var statuses)) evaluates to true.
                    if (!_expectedStatusesByScenario.TryGetValue(expectedOutcome.ScenarioId, out var statuses))
                    {
                        statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _expectedStatusesByScenario[expectedOutcome.ScenarioId] = statuses;
                    }

                    _ = statuses.Add(expectedOutcome.Status);
                }
            }

            /// <summary>
            /// Gets default compatible.
            /// </summary>
            /// <value>The default compatible value.</value>
            internal static DuckTypeAotExpectedOutcomes DefaultCompatible { get; } =
                new(
                    DuckTypeAotCompatibilityStatuses.Compatible,
                    Array.Empty<DuckTypeAotExpectedOutcome>());

            /// <summary>
            /// Gets default status.
            /// </summary>
            /// <value>The default status value.</value>
            internal string DefaultStatus { get; }

            /// <summary>
            /// Gets explicit outcomes.
            /// </summary>
            /// <value>The explicit outcomes value.</value>
            internal IReadOnlyList<DuckTypeAotExpectedOutcome> ExplicitOutcomes { get; }

            /// <summary>
            /// Attempts to try get expected statuses.
            /// </summary>
            /// <param name="scenarioId">The scenario id value.</param>
            /// <param name="statuses">The statuses value.</param>
            /// <returns>true if the operation succeeds; otherwise, false.</returns>
            internal bool TryGetExpectedStatuses(string scenarioId, out IReadOnlyCollection<string> statuses)
            {
                // Branch: take this path when (_expectedStatusesByScenario.TryGetValue(scenarioId, out var explicitStatuses)) evaluates to true.
                if (_expectedStatusesByScenario.TryGetValue(scenarioId, out var explicitStatuses))
                {
                    statuses = explicitStatuses;
                    return true;
                }

                statuses = new[] { DefaultStatus };
                return false;
            }
        }

        /// <summary>
        /// Represents duck type aot expected outcomes document.
        /// </summary>
        private sealed class DuckTypeAotExpectedOutcomesDocument
        {
            /// <summary>
            /// Gets or sets schema version.
            /// </summary>
            /// <value>The schema version value.</value>
            [JsonProperty("schemaVersion")]
            public string? SchemaVersion { get; set; }

            /// <summary>
            /// Gets or sets default status.
            /// </summary>
            /// <value>The default status value.</value>
            [JsonProperty("defaultStatus")]
            public string? DefaultStatus { get; set; }

            /// <summary>
            /// Gets or sets expected outcomes.
            /// </summary>
            /// <value>The expected outcomes value.</value>
            [JsonProperty("expectedOutcomes")]
            public List<DuckTypeAotExpectedOutcomeEntry>? ExpectedOutcomes { get; set; }

            /// <summary>
            /// Gets or sets outcomes.
            /// </summary>
            /// <value>The outcomes value.</value>
            [JsonProperty("outcomes")]
            public List<DuckTypeAotExpectedOutcomeEntry>? Outcomes { get; set; }

            /// <summary>
            /// Gets or sets expected.
            /// </summary>
            /// <value>The expected value.</value>
            [JsonProperty("expected")]
            public List<DuckTypeAotExpectedOutcomeEntry>? Expected { get; set; }
        }

        /// <summary>
        /// Represents duck type aot known limitations document.
        /// </summary>
        private sealed class DuckTypeAotKnownLimitationsDocument
        {
            /// <summary>
            /// Gets or sets known limitations.
            /// </summary>
            /// <value>The known limitations value.</value>
            [JsonProperty("knownLimitations")]
            public List<DuckTypeAotExpectedOutcomeEntry>? KnownLimitations { get; set; }

            /// <summary>
            /// Gets or sets approved limitations.
            /// </summary>
            /// <value>The approved limitations value.</value>
            [JsonProperty("approvedLimitations")]
            public List<DuckTypeAotExpectedOutcomeEntry>? ApprovedLimitations { get; set; }

            /// <summary>
            /// Gets or sets approved.
            /// </summary>
            /// <value>The approved value.</value>
            [JsonProperty("approved")]
            public List<DuckTypeAotExpectedOutcomeEntry>? Approved { get; set; }
        }

        /// <summary>
        /// Represents duck type aot expected outcome entry.
        /// </summary>
        private sealed class DuckTypeAotExpectedOutcomeEntry
        {
            /// <summary>
            /// Gets or sets scenario id.
            /// </summary>
            /// <value>The scenario id value.</value>
            [JsonProperty("scenarioId")]
            public string? ScenarioId { get; set; }

            /// <summary>
            /// Gets or sets status.
            /// </summary>
            /// <value>The status value.</value>
            [JsonProperty("status")]
            public string? Status { get; set; }
        }
    }
}
