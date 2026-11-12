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
            var useCanonicalMapContract = !StringUtil.IsNullOrWhiteSpace(options.MapFilePath);

            if (!StringUtil.IsNullOrWhiteSpace(options.CompatReportPath) && !File.Exists(options.CompatReportPath))
            {
                Utils.WriteError($"--compat-report file was not found: {options.CompatReportPath}");
                return 1;
            }

            if (!File.Exists(options.CompatMatrixPath))
            {
                Utils.WriteError($"--compat-matrix file was not found: {options.CompatMatrixPath}");
                return 1;
            }

            if (useCanonicalMapContract && !File.Exists(options.MapFilePath))
            {
                Utils.WriteError($"--map-file file was not found: {options.MapFilePath}");
                return 1;
            }

            if (!StringUtil.IsNullOrWhiteSpace(options.GenericInstantiationsPath) && !File.Exists(options.GenericInstantiationsPath))
            {
                Utils.WriteError($"--generic-instantiations file was not found: {options.GenericInstantiationsPath}");
                return 1;
            }

            if (!StringUtil.IsNullOrWhiteSpace(options.MappingCatalogPath) && !File.Exists(options.MappingCatalogPath))
            {
                Utils.WriteError($"--mapping-catalog file was not found: {options.MappingCatalogPath}");
                return 1;
            }

            if (!StringUtil.IsNullOrWhiteSpace(options.ManifestPath) && !File.Exists(options.ManifestPath))
            {
                Utils.WriteError($"--manifest file was not found: {options.ManifestPath}");
                return 1;
            }

            if (!StringUtil.IsNullOrWhiteSpace(options.ScenarioInventoryPath) && !File.Exists(options.ScenarioInventoryPath))
            {
                Utils.WriteError($"--scenario-inventory file was not found: {options.ScenarioInventoryPath}");
                return 1;
            }

            if (!useCanonicalMapContract && !StringUtil.IsNullOrWhiteSpace(options.ExpectedOutcomesPath) && !File.Exists(options.ExpectedOutcomesPath))
            {
                Utils.WriteError($"--expected-outcomes file was not found: {options.ExpectedOutcomesPath}");
                return 1;
            }

            if (!useCanonicalMapContract && !StringUtil.IsNullOrWhiteSpace(options.KnownLimitationsPath) && !File.Exists(options.KnownLimitationsPath))
            {
                Utils.WriteError($"--known-limitations file was not found: {options.KnownLimitationsPath}");
                return 1;
            }

            DuckTypeAotManifest? manifest = null;
            if (!StringUtil.IsNullOrWhiteSpace(options.ManifestPath))
            {
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
            catch (Exception ex)
            {
                Utils.WriteError($"--compat-matrix file could not be parsed: {ex.Message}");
                return 1;
            }

            if (matrix?.Mappings is null || matrix.Mappings.Count == 0)
            {
                Utils.WriteError("--compat-matrix does not contain any mappings.");
                return 1;
            }

            if (useCanonicalMapContract)
            {
                var mapFileResult = DuckTypeAotMapFileParser.Parse(options.MapFilePath);
                if (mapFileResult.Errors.Count > 0)
                {
                    foreach (var error in mapFileResult.Errors)
                    {
                        Utils.WriteError(error);
                    }

                    return 1;
                }

                if (mapFileResult.Mappings.Count == 0)
                {
                    Utils.WriteError("--map-file does not contain any mappings.");
                    return 1;
                }

                var expectedMappings = mapFileResult.Mappings.ToDictionary(mapping => mapping.Key, StringComparer.Ordinal);
                var expansionErrors = new List<string>();
                IEnumerable<DuckTypeAotTypeReference> genericRoots;
                if (!StringUtil.IsNullOrWhiteSpace(options.GenericInstantiationsPath))
                {
                    var rootsResult = DuckTypeAotGenericInstantiationsParser.Parse(options.GenericInstantiationsPath!);
                    expansionErrors.AddRange(rootsResult.Errors);
                    genericRoots = rootsResult.TypeRoots;
                }
                else
                {
                    genericRoots = manifest?.GenericInstantiations?
                                           .Where(root => !StringUtil.IsNullOrWhiteSpace(root.Type) && !StringUtil.IsNullOrWhiteSpace(root.Assembly))
                                           .Select(root => new DuckTypeAotTypeReference(root.Type!, root.Assembly!))
                                ?? Enumerable.Empty<DuckTypeAotTypeReference>();
                }

                DuckTypeAotMappingResolver.ExpandOpenGenericMappings(expectedMappings, genericRoots, expansionErrors);
                DuckTypeAotMappingResolver.ValidateGenericClosure(expectedMappings.Values, expansionErrors);
                if (expansionErrors.Count > 0)
                {
                    foreach (var error in expansionErrors)
                    {
                        Utils.WriteError(error);
                    }

                    return 1;
                }

                if (!ValidateMapFileContract(matrix, expectedMappings.Values.ToList()))
                {
                    return 1;
                }
            }
            else
            {
                if (!ValidateLegacyOverrideContractsAreStrictEmpty(options))
                {
                    return 1;
                }

                if (StringUtil.IsNullOrWhiteSpace(options.MappingCatalogPath) && !ValidateNoNonCompatibleMappings(matrix))
                {
                    return 1;
                }
            }

            if (manifest is not null)
            {
                if (!ValidateManifest(matrix, manifest))
                {
                    return 1;
                }

                if (!ValidateManifestAssemblyFingerprints(manifest, options.StrictAssemblyFingerprintValidation))
                {
                    return 1;
                }

                if (!ValidateManifestGeneratedArtifacts(manifest, options.StrictAssemblyFingerprintValidation))
                {
                    return 1;
                }

                if (!ValidateTrimmerDescriptorCoupling(matrix, manifest))
                {
                    return 1;
                }
            }

            IReadOnlyList<string>? mappingCatalogScenarioIds = null;
            if (!StringUtil.IsNullOrWhiteSpace(options.MappingCatalogPath))
            {
                if (!ValidateMappingCatalog(
                    matrix,
                    options.MappingCatalogPath!,
                    enforceScenarioIdMatchesMatrix: !useCanonicalMapContract,
                    enforceCatalogCoversMatrix: useCanonicalMapContract,
                    out mappingCatalogScenarioIds))
                {
                    return 1;
                }
            }

            if (!StringUtil.IsNullOrWhiteSpace(options.ScenarioInventoryPath))
            {
                if (useCanonicalMapContract && mappingCatalogScenarioIds is not null)
                {
                    if (!ValidateScenarioInventory(mappingCatalogScenarioIds, options.ScenarioInventoryPath!, "--mapping-catalog"))
                    {
                        return 1;
                    }
                }
                else if (!ValidateScenarioInventory(matrix, options.ScenarioInventoryPath!))
                {
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Validates legacy override contracts are strict empty.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateLegacyOverrideContractsAreStrictEmpty(DuckTypeAotVerifyCompatOptions options)
        {
            if (!StringUtil.IsNullOrWhiteSpace(options.ExpectedOutcomesPath))
            {
                if (!TryReadExpectedOutcomes(options.ExpectedOutcomesPath!, out var expectedOutcomes))
                {
                    return false;
                }

                if (!string.Equals(expectedOutcomes.DefaultStatus, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase) ||
                    expectedOutcomes.ExplicitOutcomes.Count > 0)
                {
                    Utils.WriteError("--expected-outcomes is legacy-only in strict parity mode and must remain empty with defaultStatus='compatible'.");
                    return false;
                }

                Utils.WriteWarning("--expected-outcomes is deprecated and non-authoritative in strict parity mode.");
            }

            if (!StringUtil.IsNullOrWhiteSpace(options.KnownLimitationsPath))
            {
                Utils.WriteWarning("--known-limitations is deprecated and non-authoritative in strict parity mode.");
                if (!TryReadLegacyKnownLimitationsAsExpectedOutcomes(options.KnownLimitationsPath!, out var knownLimitations))
                {
                    return false;
                }

                if (knownLimitations.ExplicitOutcomes.Count > 0)
                {
                    Utils.WriteError("--known-limitations is legacy-only in strict parity mode and must remain empty.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates no non compatible mappings.
        /// </summary>
        /// <param name="matrix">The matrix value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateNoNonCompatibleMappings(DuckTypeAotCompatibilityMatrix matrix)
        {
            var errors = new List<string>();
            foreach (var mapping in matrix.Mappings)
            {
                var status = mapping.Status ?? string.Empty;
                if (mapping.BehavesLikeDynamicDuckTyping)
                {
                    continue;
                }

                errors.Add(
                    "--compat-matrix contains non-compatible mapping in strict parity mode: " +
                    $"scenario='{mapping.Id ?? "(null)"}', mode='{mapping.Mode ?? "(null)"}', " +
                    $"proxy='{mapping.ProxyType ?? "(null)"}', target='{mapping.TargetType ?? "(null)"}', status='{status}'.");
            }

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
        /// Validates that the compatibility matrix exactly matches the canonical map-file contract and all mapped statuses are compatible.
        /// </summary>
        /// <param name="matrix">The compatibility matrix.</param>
        /// <param name="mapMappings">The canonical map mappings.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateMapFileContract(
            DuckTypeAotCompatibilityMatrix matrix,
            IReadOnlyList<DuckTypeAotMapping> mapMappings)
        {
            var errors = new List<string>();
            var matrixByKey = new Dictionary<string, DuckTypeAotCompatibilityMapping>(StringComparer.Ordinal);
            foreach (var matrixMapping in matrix.Mappings)
            {
                if (!TryBuildCompatibilityMappingKey(matrixMapping, out var mappingKey, out var error))
                {
                    errors.Add(error);
                    continue;
                }

                if (!matrixByKey.TryAdd(mappingKey, matrixMapping))
                {
                    errors.Add($"--compat-matrix contains duplicate mappings for key '{mappingKey}'.");
                }
            }

            var mapByKey = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);
            foreach (var mapMapping in mapMappings)
            {
                if (!mapByKey.TryAdd(mapMapping.Key, mapMapping))
                {
                    errors.Add($"--map-file contains duplicate mappings for key '{mapMapping.Key}'.");
                }
            }

            foreach (var mapEntry in mapByKey)
            {
                if (!matrixByKey.TryGetValue(mapEntry.Key, out var matrixMapping))
                {
                    errors.Add(
                        $"--compat-matrix is missing mapping declared in --map-file: " +
                        $"mode={mapEntry.Value.Mode}, proxy={mapEntry.Value.ProxyTypeName}, target={mapEntry.Value.TargetTypeName}.");
                    continue;
                }

                var status = matrixMapping.Status ?? string.Empty;
                if (!matrixMapping.BehavesLikeDynamicDuckTyping)
                {
                    errors.Add(
                        $"Mapped entry is not compatible: key='{mapEntry.Key}', status='{status}', " +
                        $"diagnostic='{matrixMapping.DiagnosticCode ?? "(none)"}'.");
                }
            }

            foreach (var matrixEntry in matrixByKey)
            {
                if (mapByKey.ContainsKey(matrixEntry.Key))
                {
                    continue;
                }

                errors.Add(
                    $"--compat-matrix contains mapping that is not declared in --map-file: key='{matrixEntry.Key}', " +
                    $"id='{matrixEntry.Value.Id ?? "(null)"}'.");
            }

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
                Utils.WriteError($"--expected-outcomes file could not be parsed ({expectedOutcomesPath}): {ex.Message}");
                return false;
            }

            if (expectedOutcomesDocument is null)
            {
                Utils.WriteError($"--expected-outcomes file is empty or invalid JSON: {expectedOutcomesPath}");
                return false;
            }

            var defaultStatus = StringUtil.IsNullOrWhiteSpace(expectedOutcomesDocument.DefaultStatus)
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
                Utils.WriteError($"--known-limitations file could not be parsed ({knownLimitationsPath}): {ex.Message}");
                return false;
            }

            if (knownLimitationsDocument is null)
            {
                Utils.WriteError($"--known-limitations file is empty or invalid JSON: {knownLimitationsPath}");
                return false;
            }

            var entries = knownLimitationsDocument.KnownLimitations
                          ?? knownLimitationsDocument.ApprovedLimitations
                          ?? knownLimitationsDocument.Approved
                          ?? new List<DuckTypeAotExpectedOutcomeEntry>();

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

            if (StringUtil.IsNullOrWhiteSpace(defaultStatus))
            {
                errors.Add($"{optionName} defaultStatus must be non-empty in '{sourcePath}'.");
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var scenarioId = entry?.ScenarioId?.Trim();
                var status = entry?.Status?.Trim();
                if (StringUtil.IsNullOrWhiteSpace(scenarioId) || StringUtil.IsNullOrWhiteSpace(status))
                {
                    errors.Add($"{optionName} entry #{i + 1} in '{sourcePath}' must include non-empty scenarioId and status.");
                    continue;
                }

                var pairKey = $"{scenarioId}|{status}";
                if (!seenEntries.Add(pairKey))
                {
                    errors.Add($"{optionName} contains duplicate entry '{pairKey}' in '{sourcePath}'.");
                    continue;
                }

                normalizedEntries.Add(new DuckTypeAotExpectedOutcome(scenarioId!, status!));
            }

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
        /// <param name="enforceScenarioIdMatchesMatrix">Whether required mapping scenario ids must match matrix ids.</param>
        /// <param name="enforceCatalogCoversMatrix">Whether the catalog must cover every matrix mapping.</param>
        /// <param name="scenarioIds">The scenario ids listed in the catalog.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateMappingCatalog(
            DuckTypeAotCompatibilityMatrix matrix,
            string mappingCatalogPath,
            bool enforceScenarioIdMatchesMatrix,
            bool enforceCatalogCoversMatrix,
            out IReadOnlyList<string> scenarioIds)
        {
            scenarioIds = Array.Empty<string>();
            var catalogResult = DuckTypeAotMappingCatalogParser.Parse(mappingCatalogPath);
            if (catalogResult.Errors.Count > 0)
            {
                foreach (var error in catalogResult.Errors)
                {
                    Utils.WriteError(error);
                }

                return false;
            }

            var errors = new List<string>();
            var catalogScenarioIds = new List<string>();
            var catalogMappingKeys = new HashSet<string>(StringComparer.Ordinal);
            var matrixMappingByKey = new Dictionary<string, DuckTypeAotCompatibilityMapping>(StringComparer.Ordinal);
            foreach (var matrixMapping in matrix.Mappings)
            {
                if (!TryBuildCompatibilityMappingKey(matrixMapping, out var mappingKey, out var error))
                {
                    errors.Add(error);
                    continue;
                }

                if (!matrixMappingByKey.TryAdd(mappingKey, matrixMapping))
                {
                    errors.Add($"--compat-matrix contains duplicate mappings for key '{mappingKey}'.");
                }
            }

            foreach (var requiredMappingExpectation in catalogResult.RequiredMappingExpectations)
            {
                var requiredMapping = requiredMappingExpectation.Mapping;
                _ = catalogMappingKeys.Add(requiredMapping.Key);
                if (StringUtil.IsNullOrWhiteSpace(requiredMapping.ScenarioId))
                {
                    errors.Add(
                        $"--mapping-catalog required mapping is missing scenarioId: " +
                        $"mode={requiredMapping.Mode}, proxy={requiredMapping.ProxyTypeName}, target={requiredMapping.TargetTypeName}.");
                    continue;
                }

                catalogScenarioIds.Add(requiredMapping.ScenarioId!);

                if (!matrixMappingByKey.TryGetValue(requiredMapping.Key, out var matrixMapping))
                {
                    errors.Add(
                        $"--compat-matrix is missing required mapping from --mapping-catalog: " +
                        $"mode={requiredMapping.Mode}, proxy={requiredMapping.ProxyTypeName}, target={requiredMapping.TargetTypeName}.");
                    continue;
                }

                var actualStatus = matrixMapping.Status ?? string.Empty;
                var expectedStatus = requiredMappingExpectation.ExpectedStatus;
                // A mapping expected to be compatible can also replay the failure dynamic duck typing has: it behaves the same in
                // both modes.
                if (!string.Equals(actualStatus, expectedStatus, StringComparison.OrdinalIgnoreCase) &&
                    !(matrixMapping.DynamicFailureReplayed && string.Equals(expectedStatus, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(
                        $"Required mapping status mismatch in strict parity mode: " +
                        $"key='{requiredMapping.Key}', scenario='{matrixMapping.Id ?? "(null)"}', expected='{expectedStatus}', actual='{actualStatus}'.");
                }

                if (enforceScenarioIdMatchesMatrix &&
                    !StringUtil.IsNullOrWhiteSpace(requiredMapping.ScenarioId) &&
                    !string.Equals(matrixMapping.Id, requiredMapping.ScenarioId, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Scenario id mismatch for required mapping '{requiredMapping.Key}'. " +
                        $"Expected='{requiredMapping.ScenarioId}', actual='{matrixMapping.Id ?? "(null)"}'.");
                }
            }

            if (enforceCatalogCoversMatrix)
            {
                foreach (var matrixEntry in matrixMappingByKey)
                {
                    if (catalogMappingKeys.Contains(matrixEntry.Key))
                    {
                        continue;
                    }

                    var matrixMapping = matrixEntry.Value;
                    errors.Add(
                        $"--mapping-catalog is missing mapping declared in --map-file/--compat-matrix: " +
                        $"mode={matrixMapping.Mode ?? "(null)"}, proxy={matrixMapping.ProxyType ?? "(null)"}, target={matrixMapping.TargetType ?? "(null)"}.");
                }
            }

            if (errors.Count == 0)
            {
                scenarioIds = catalogScenarioIds;
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
            var errors = new List<string>();
            var matrixScenarioIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < matrix.Mappings.Count; i++)
            {
                var mapping = matrix.Mappings[i];
                if (StringUtil.IsNullOrWhiteSpace(mapping.Id))
                {
                    errors.Add(
                        $"--compat-matrix mapping entry #{i + 1} is missing scenario id while --scenario-inventory is enabled. " +
                        $"proxy='{mapping.ProxyType ?? "(null)"}', target='{mapping.TargetType ?? "(null)"}'.");
                    continue;
                }

                _ = matrixScenarioIds.Add(mapping.Id!);
            }

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Utils.WriteError(error);
                }

                return false;
            }

            return ValidateScenarioInventory(matrixScenarioIds, scenarioInventoryPath, "--compat-matrix");
        }

        /// <summary>
        /// Validates validate scenario inventory.
        /// </summary>
        /// <param name="scenarioIds">The scenario ids value.</param>
        /// <param name="scenarioInventoryPath">The scenario inventory path value.</param>
        /// <param name="scenarioSourceName">The scenario source name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateScenarioInventory(IReadOnlyList<string> scenarioIds, string scenarioInventoryPath, string scenarioSourceName)
        {
            return ValidateScenarioInventory(new HashSet<string>(scenarioIds, StringComparer.Ordinal), scenarioInventoryPath, scenarioSourceName);
        }

        /// <summary>
        /// Validates validate scenario inventory.
        /// </summary>
        /// <param name="scenarioIds">The scenario ids value.</param>
        /// <param name="scenarioInventoryPath">The scenario inventory path value.</param>
        /// <param name="scenarioSourceName">The scenario source name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ValidateScenarioInventory(ISet<string> scenarioIds, string scenarioInventoryPath, string scenarioSourceName)
        {
            var inventoryResult = DuckTypeAotScenarioInventoryParser.Parse(scenarioInventoryPath);
            if (inventoryResult.Errors.Count > 0)
            {
                foreach (var error in inventoryResult.Errors)
                {
                    Utils.WriteError(error);
                }

                return false;
            }

            var errors = new List<string>();
            foreach (var requiredEntry in inventoryResult.RequiredScenarios)
            {
                if (!IsScenarioCoveredByMatrix(requiredEntry, scenarioIds))
                {
                    errors.Add($"{scenarioSourceName} is missing required scenario from --scenario-inventory: '{requiredEntry}'.");
                }
            }

            foreach (var scenarioId in scenarioIds)
            {
                if (!IsScenarioTrackedByInventory(scenarioId, inventoryResult.RequiredScenarios))
                {
                    errors.Add(
                        $"{scenarioSourceName} contains scenario id '{scenarioId}' that is not tracked by --scenario-inventory. " +
                        "Add it to the inventory (or matching wildcard group) to avoid unreviewed scenario drift.");
                }
            }

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
            if (!IsWildcardScenarioEntry(requiredEntry))
            {
                return matrixScenarioIds.Contains(requiredEntry);
            }

            var wildcardPrefix = requiredEntry.Substring(0, requiredEntry.Length - 1);
            foreach (var matrixScenarioId in matrixScenarioIds)
            {
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
                if (!IsWildcardScenarioEntry(requiredEntry))
                {
                    if (string.Equals(matrixScenarioId, requiredEntry, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    continue;
                }

                var wildcardPrefix = requiredEntry.Substring(0, requiredEntry.Length - 1);
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
                Utils.WriteError($"--manifest file could not be parsed: {ex.Message}");
                manifest = null;
                return false;
            }

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

            if (!StringUtil.IsNullOrWhiteSpace(matrix.SchemaVersion) &&
                !StringUtil.IsNullOrWhiteSpace(manifest.SchemaVersion) &&
                !string.Equals(matrix.SchemaVersion, manifest.SchemaVersion, StringComparison.Ordinal))
            {
                errors.Add($"Schema version mismatch between --compat-matrix and --manifest. Matrix='{matrix.SchemaVersion}', manifest='{manifest.SchemaVersion}'.");
            }

            var matrixMappingByKey = new Dictionary<string, DuckTypeAotCompatibilityMapping>(StringComparer.Ordinal);
            foreach (var matrixMapping in matrix.Mappings)
            {
                if (!TryBuildCompatibilityMappingKey(matrixMapping, out var mappingKey, out var error))
                {
                    errors.Add(error);
                    continue;
                }

                if (!matrixMappingByKey.TryAdd(mappingKey, matrixMapping))
                {
                    errors.Add($"--compat-matrix contains duplicate mappings for key '{mappingKey}'.");
                }
            }

            var manifestMappingByKey = new Dictionary<string, DuckTypeAotManifestMapping>(StringComparer.Ordinal);
            foreach (var manifestMapping in manifest.Mappings)
            {
                if (!TryBuildManifestMappingKey(manifestMapping, out var mappingKey, out var error))
                {
                    errors.Add(error);
                    continue;
                }

                if (!manifestMappingByKey.TryAdd(mappingKey, manifestMapping))
                {
                    errors.Add($"--manifest contains duplicate mappings for key '{mappingKey}'.");
                }
            }

            foreach (var (mappingKey, matrixMapping) in matrixMappingByKey)
            {
                if (!manifestMappingByKey.TryGetValue(mappingKey, out var manifestMapping))
                {
                    errors.Add($"--manifest is missing mapping from --compat-matrix: key='{mappingKey}'.");
                    continue;
                }

                if (!ValidateChecksum(matrixMapping.MappingIdentityChecksum, out var matrixChecksumError))
                {
                    errors.Add($"--compat-matrix mapping id '{matrixMapping.Id ?? "(null)"}' has invalid mappingIdentityChecksum: {matrixChecksumError}");
                    continue;
                }

                if (!ValidateChecksum(manifestMapping.MappingIdentityChecksum, out var manifestChecksumError))
                {
                    errors.Add($"--manifest mapping key '{mappingKey}' has invalid mappingIdentityChecksum: {manifestChecksumError}");
                    continue;
                }

                if (!string.Equals(matrixMapping.MappingIdentityChecksum, manifestMapping.MappingIdentityChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"mappingIdentityChecksum mismatch for mapping '{mappingKey}'. " +
                        $"Matrix='{matrixMapping.MappingIdentityChecksum}', manifest='{manifestMapping.MappingIdentityChecksum}'.");
                }

                if (!StringUtil.IsNullOrWhiteSpace(manifestMapping.ScenarioId) &&
                    !string.Equals(matrixMapping.Id, manifestMapping.ScenarioId, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Scenario id mismatch between --compat-matrix and --manifest for mapping '{mappingKey}'. " +
                        $"Matrix='{matrixMapping.Id ?? "(null)"}', manifest='{manifestMapping.ScenarioId}'.");
                }
            }

            foreach (var mappingKey in manifestMappingByKey.Keys)
            {
                if (!matrixMappingByKey.ContainsKey(mappingKey))
                {
                    errors.Add($"--compat-matrix is missing mapping from --manifest: key='{mappingKey}'.");
                }
            }

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
            if (manifest.DatadogTraceAssembly is not null)
            {
                ValidateFingerprints(new[] { manifest.DatadogTraceAssembly }, "Datadog.Trace", issues);
            }

            if (issues.Count == 0)
            {
                return true;
            }

            foreach (var issue in issues)
            {
                if (strictAssemblyFingerprintValidation)
                {
                    Utils.WriteError(issue);
                }
                else
                {
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

            if (!StringUtil.IsNullOrWhiteSpace(manifest.RegistryAssembly) && File.Exists(manifest.RegistryAssembly))
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(manifest.RegistryAssembly);
                    var actualVersion = assemblyName.Version?.ToString() ?? "0.0.0.0";
                    var actualPublicKeyTokenBytes = assemblyName.GetPublicKeyToken();
                    var actualPublicKeyToken = actualPublicKeyTokenBytes is { Length: > 0 }
                                                   ? BitConverter.ToString(actualPublicKeyTokenBytes).Replace("-", string.Empty).ToLowerInvariant()
                                                   : string.Empty;
                    var actualIsStrongNameSigned = !StringUtil.IsNullOrWhiteSpace(actualPublicKeyToken);

                    if (!StringUtil.IsNullOrWhiteSpace(manifest.RegistryAssemblyVersion) &&
                        !string.Equals(actualVersion, manifest.RegistryAssemblyVersion, StringComparison.Ordinal))
                    {
                        issues.Add($"Manifest registry assembly version mismatch. Expected '{manifest.RegistryAssemblyVersion}', got '{actualVersion}'.");
                    }

                    if (manifest.RegistryStrongNameSigned.HasValue &&
                        manifest.RegistryStrongNameSigned.Value != actualIsStrongNameSigned)
                    {
                        issues.Add(
                            $"Manifest registry strong-name flag mismatch. " +
                            $"Expected '{manifest.RegistryStrongNameSigned.Value}', got '{actualIsStrongNameSigned}'.");
                    }

                    if (!StringUtil.IsNullOrWhiteSpace(manifest.RegistryPublicKeyToken))
                    {
                        if (!actualIsStrongNameSigned)
                        {
                            issues.Add(
                                $"Manifest registry public key token is set ('{manifest.RegistryPublicKeyToken}') but registry assembly is not strong-name signed.");
                        }
                        else if (!string.Equals(actualPublicKeyToken, manifest.RegistryPublicKeyToken, StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add(
                                $"Manifest registry public key token mismatch. Expected '{manifest.RegistryPublicKeyToken}', got '{actualPublicKeyToken}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    issues.Add($"Manifest registry assembly metadata could not be validated: {ex.Message}");
                }
            }

            if (issues.Count == 0)
            {
                return true;
            }

            foreach (var issue in issues)
            {
                if (strictAssemblyFingerprintValidation)
                {
                    Utils.WriteError(issue);
                }
                else
                {
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
            if (StringUtil.IsNullOrWhiteSpace(manifest.TrimmerDescriptorPath))
            {
                return true;
            }

            if (!TryReadTrimmerDescriptorRoots(manifest.TrimmerDescriptorPath!, out var rootsByAssembly, out var readError))
            {
                Utils.WriteError(readError);
                return false;
            }

            var errors = new List<string>();

            if (!StringUtil.IsNullOrWhiteSpace(manifest.RegistryAssemblyName) &&
                !StringUtil.IsNullOrWhiteSpace(manifest.RegistryBootstrapType))
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
                // A mapping that fails only for other runtime types of its target still registers its proxy type.
                if (!string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase) && !mapping.FailsOnlyForOtherRuntimeTypes)
                {
                    continue;
                }

                if (!StringUtil.IsNullOrWhiteSpace(mapping.ProxyAssembly) && !StringUtil.IsNullOrWhiteSpace(mapping.ProxyType))
                {
                    ValidateTrimmerDescriptorRoot(
                        rootsByAssembly,
                        mapping.ProxyAssembly!,
                        mapping.ProxyType!,
                        $"compatible mapping '{mapping.Id ?? "(null)"}' proxy root",
                        errors);
                }

                // Target types and generated proxy types aren't rooted (see DuckTypeAotArtifactsWriter.WriteTrimmerDescriptor).
            }

            if (manifest.GenericInstantiations is not null)
            {
                foreach (var typeReference in manifest.GenericInstantiations)
                {
                    if (StringUtil.IsNullOrWhiteSpace(typeReference.Assembly) || StringUtil.IsNullOrWhiteSpace(typeReference.Type))
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

            if (!File.Exists(descriptorPath))
            {
                error = $"Manifest trimmer descriptor path was not found: {descriptorPath}";
                return false;
            }

            try
            {
                var document = XDocument.Load(descriptorPath);
                var linker = document.Root;
                if (linker is null || !string.Equals(linker.Name.LocalName, "linker", StringComparison.Ordinal))
                {
                    error = $"Trimmer descriptor is invalid (missing <linker> root): {descriptorPath}";
                    return false;
                }

                foreach (var assemblyElement in linker.Elements())
                {
                    if (!string.Equals(assemblyElement.Name.LocalName, "assembly", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var assemblyName = assemblyElement.Attribute("fullname")?.Value;
                    if (StringUtil.IsNullOrWhiteSpace(assemblyName))
                    {
                        continue;
                    }

                    if (!rootsByAssembly.TryGetValue(assemblyName!, out var typeRoots))
                    {
                        typeRoots = new HashSet<string>(StringComparer.Ordinal);
                        rootsByAssembly[assemblyName!] = typeRoots;
                    }

                    foreach (var typeElement in assemblyElement.Elements())
                    {
                        if (!string.Equals(typeElement.Name.LocalName, "type", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var typeName = typeElement.Attribute("fullname")?.Value;
                        if (StringUtil.IsNullOrWhiteSpace(typeName))
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
            // Closed generics and arrays aren't rooted by name (see GetTrimmerDescriptorTypeName).
            if (DuckTypeAotNameHelpers.GetTrimmerDescriptorTypeName(typeName) is not { } normalizedTypeName)
            {
                return;
            }

            if (!rootsByAssembly.TryGetValue(assemblyName, out var typeRoots))
            {
                errors.Add($"Trimmer descriptor is missing assembly root '{assemblyName}' required for {context}.");
                return;
            }

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
        private static void ValidateFileFingerprint(string? path, string? expectedSha256, string artifactName, ICollection<string> issues)
        {
            if (StringUtil.IsNullOrWhiteSpace(path))
            {
                issues.Add($"Manifest {artifactName} path is missing.");
                return;
            }

            var resolvedPath = path!;
            if (!File.Exists(resolvedPath))
            {
                issues.Add($"Manifest {artifactName} path was not found: {resolvedPath}");
                return;
            }

            if (!ValidateChecksum(expectedSha256, out var checksumError))
            {
                issues.Add($"Manifest {artifactName} has invalid sha256 for '{resolvedPath}': {checksumError}.");
                return;
            }

            var actualSha256 = ComputeSha256(resolvedPath);
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
        private static bool TryBuildCompatibilityMappingKey(
            DuckTypeAotCompatibilityMapping mapping,
            out string key,
            out string error)
        {
            key = string.Empty;
            error = string.Empty;

            if (!TryParseMode(mapping.Mode, out var mode))
            {
                error = $"--compat-matrix mapping id '{mapping.Id ?? "(null)"}' has invalid mode '{mapping.Mode ?? "(null)"}'.";
                return false;
            }

            if (StringUtil.IsNullOrWhiteSpace(mapping.ProxyType) ||
                StringUtil.IsNullOrWhiteSpace(mapping.ProxyAssembly) ||
                StringUtil.IsNullOrWhiteSpace(mapping.TargetType) ||
                StringUtil.IsNullOrWhiteSpace(mapping.TargetAssembly))
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
        private static bool TryBuildManifestMappingKey(
            DuckTypeAotManifestMapping mapping,
            out string key,
            out string error)
        {
            key = string.Empty;
            error = string.Empty;

            if (!TryParseMode(mapping.Mode, out var mode))
            {
                error = $"--manifest mapping has invalid mode '{mapping.Mode ?? "(null)"}'.";
                return false;
            }

            if (StringUtil.IsNullOrWhiteSpace(mapping.ProxyType) ||
                StringUtil.IsNullOrWhiteSpace(mapping.ProxyAssembly) ||
                StringUtil.IsNullOrWhiteSpace(mapping.TargetType) ||
                StringUtil.IsNullOrWhiteSpace(mapping.TargetAssembly))
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
            if (StringUtil.IsNullOrWhiteSpace(value))
            {
                error = "value is empty";
                return false;
            }

            var checksum = value!;
            if (checksum.Length != 64)
            {
                error = $"value must be 64 hex chars, got length {checksum.Length}";
                return false;
            }

            for (var i = 0; i < checksum.Length; i++)
            {
                var c = checksum[i];
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
            if (fingerprints is null || fingerprints.Count == 0)
            {
                return;
            }

            foreach (var fingerprint in fingerprints)
            {
                var expectedName = fingerprint.Name ?? "(unknown)";
                var assemblyPath = fingerprint.Path;
                if (StringUtil.IsNullOrWhiteSpace(assemblyPath))
                {
                    issues.Add($"Manifest {assemblyKind} assembly '{expectedName}' is missing path.");
                    continue;
                }

                var resolvedAssemblyPath = assemblyPath!;
                if (!File.Exists(resolvedAssemblyPath))
                {
                    issues.Add($"Manifest {assemblyKind} assembly '{expectedName}' path was not found: {resolvedAssemblyPath}");
                    continue;
                }

                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(resolvedAssemblyPath);
                    var actualName = assemblyName.Name ?? string.Empty;
                    if (!StringUtil.IsNullOrWhiteSpace(fingerprint.Name) &&
                        !string.Equals(actualName, fingerprint.Name, StringComparison.Ordinal))
                    {
                        issues.Add($"Manifest {assemblyKind} assembly name mismatch for '{resolvedAssemblyPath}'. Expected '{fingerprint.Name}', got '{actualName}'.");
                    }

                    using var module = ModuleDefMD.Load(resolvedAssemblyPath);
                    var actualMvid = module.Mvid?.ToString("D") ?? string.Empty;
                    if (!StringUtil.IsNullOrWhiteSpace(fingerprint.Mvid) &&
                        !string.Equals(actualMvid, fingerprint.Mvid, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add($"Manifest {assemblyKind} assembly MVID mismatch for '{resolvedAssemblyPath}'. Expected '{fingerprint.Mvid}', got '{actualMvid}'.");
                    }

                    if (!ValidateChecksum(fingerprint.Sha256, out var checksumError))
                    {
                        issues.Add($"Manifest {assemblyKind} assembly has invalid sha256 for '{resolvedAssemblyPath}': {checksumError}.");
                    }
                    else
                    {
                        var actualSha256 = ComputeSha256(resolvedAssemblyPath);
                        if (!string.Equals(actualSha256, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add($"Manifest {assemblyKind} assembly sha256 mismatch for '{resolvedAssemblyPath}'. Expected '{fingerprint.Sha256}', got '{actualSha256}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
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
        /// Attempts to try parse mode.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <param name="mode">The mode value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryParseMode(string? value, out DuckTypeAotMappingMode mode)
        {
            if (string.Equals(value, "forward", StringComparison.OrdinalIgnoreCase))
            {
                mode = DuckTypeAotMappingMode.Forward;
                return true;
            }

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
            /// Initializes a new instance of the <see cref="DuckTypeAotExpectedOutcomes"/> class.
            /// </summary>
            /// <param name="defaultStatus">The default status value.</param>
            /// <param name="explicitOutcomes">The explicit outcomes value.</param>
            internal DuckTypeAotExpectedOutcomes(string defaultStatus, IReadOnlyList<DuckTypeAotExpectedOutcome> explicitOutcomes)
            {
                DefaultStatus = defaultStatus;
                ExplicitOutcomes = explicitOutcomes;
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
