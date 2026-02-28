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
    internal static class DuckTypeAotVerifyCompatProcessor
    {
        internal static int Process(DuckTypeAotVerifyCompatOptions options)
        {
            if (!File.Exists(options.CompatReportPath))
            {
                Utils.WriteError($"--compat-report file was not found: {options.CompatReportPath}");
                return 1;
            }

            if (!File.Exists(options.CompatMatrixPath))
            {
                Utils.WriteError($"--compat-matrix file was not found: {options.CompatMatrixPath}");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(options.MappingCatalogPath) && !File.Exists(options.MappingCatalogPath))
            {
                Utils.WriteError($"--mapping-catalog file was not found: {options.MappingCatalogPath}");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(options.ManifestPath) && !File.Exists(options.ManifestPath))
            {
                Utils.WriteError($"--manifest file was not found: {options.ManifestPath}");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(options.ScenarioInventoryPath) && !File.Exists(options.ScenarioInventoryPath))
            {
                Utils.WriteError($"--scenario-inventory file was not found: {options.ScenarioInventoryPath}");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(options.ExpectedOutcomesPath) && !File.Exists(options.ExpectedOutcomesPath))
            {
                Utils.WriteError($"--expected-outcomes file was not found: {options.ExpectedOutcomesPath}");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(options.KnownLimitationsPath) && !File.Exists(options.KnownLimitationsPath))
            {
                Utils.WriteError($"--known-limitations file was not found: {options.KnownLimitationsPath}");
                return 1;
            }

            DuckTypeAotManifest? manifest = null;
            if (!string.IsNullOrWhiteSpace(options.ManifestPath))
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
            catch (System.Exception ex)
            {
                Utils.WriteError($"--compat-matrix file could not be parsed: {ex.Message}");
                return 1;
            }

            if (matrix?.Mappings is null || matrix.Mappings.Count == 0)
            {
                Utils.WriteError("--compat-matrix does not contain any mappings.");
                return 1;
            }

            var expectedOutcomes = DuckTypeAotExpectedOutcomes.DefaultCompatible;
            if (!string.IsNullOrWhiteSpace(options.ExpectedOutcomesPath))
            {
                if (!TryReadExpectedOutcomes(options.ExpectedOutcomesPath!, out expectedOutcomes))
                {
                    return 1;
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.KnownLimitationsPath))
            {
                Utils.WriteWarning("--known-limitations is deprecated. Use --expected-outcomes instead.");
                if (!TryReadLegacyKnownLimitationsAsExpectedOutcomes(options.KnownLimitationsPath!, out expectedOutcomes))
                {
                    return 1;
                }
            }

            if (!ValidateExpectedOutcomes(matrix, expectedOutcomes))
            {
                return 1;
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

            if (!string.IsNullOrWhiteSpace(options.MappingCatalogPath))
            {
                if (!ValidateMappingCatalog(matrix, options.MappingCatalogPath!, expectedOutcomes))
                {
                    return 1;
                }
            }

            if (!string.IsNullOrWhiteSpace(options.ScenarioInventoryPath))
            {
                if (!ValidateScenarioInventory(matrix, options.ScenarioInventoryPath!))
                {
                    return 1;
                }
            }

            return 0;
        }

        private static bool ValidateExpectedOutcomes(
            DuckTypeAotCompatibilityMatrix matrix,
            DuckTypeAotExpectedOutcomes expectedOutcomes)
        {
            var errors = new List<string>();
            var observedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < matrix.Mappings.Count; i++)
            {
                var mapping = matrix.Mappings[i];
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
                if (!observedPairs.Contains(expectedPair))
                {
                    errors.Add(
                        $"--expected-outcomes entry is stale or mismatched: scenario='{expectedOutcome.ScenarioId}', status='{expectedOutcome.Status}'.");
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

            var defaultStatus = string.IsNullOrWhiteSpace(expectedOutcomesDocument.DefaultStatus)
                                    ? DuckTypeAotCompatibilityStatuses.Compatible
                                    : expectedOutcomesDocument.DefaultStatus!.Trim();
            var entries = expectedOutcomesDocument.ExpectedOutcomes
                          ?? expectedOutcomesDocument.Outcomes
                          ?? expectedOutcomesDocument.Expected
                          ?? new List<DuckTypeAotExpectedOutcomeEntry>();

            return TryBuildExpectedOutcomes(entries, defaultStatus, expectedOutcomesPath, "--expected-outcomes", out expectedOutcomes);
        }

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
            if (entries.Count == 0)
            {
                Utils.WriteError($"--known-limitations does not contain any entries: {knownLimitationsPath}");
                return false;
            }

            return TryBuildExpectedOutcomes(entries, DuckTypeAotCompatibilityStatuses.Compatible, knownLimitationsPath, "--known-limitations", out expectedOutcomes);
        }

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

            if (string.IsNullOrWhiteSpace(defaultStatus))
            {
                errors.Add($"{optionName} defaultStatus must be non-empty in '{sourcePath}'.");
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var scenarioId = entry?.ScenarioId?.Trim();
                var status = entry?.Status?.Trim();
                if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(status))
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

        private static bool ValidateMappingCatalog(
            DuckTypeAotCompatibilityMatrix matrix,
            string mappingCatalogPath,
            DuckTypeAotExpectedOutcomes expectedOutcomes)
        {
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

            foreach (var requiredMapping in catalogResult.RequiredMappings)
            {
                if (string.IsNullOrWhiteSpace(requiredMapping.ScenarioId))
                {
                    errors.Add(
                        $"--mapping-catalog required mapping is missing scenarioId: " +
                        $"mode={requiredMapping.Mode}, proxy={requiredMapping.ProxyTypeName}, target={requiredMapping.TargetTypeName}.");
                    continue;
                }

                if (!matrixMappingByKey.TryGetValue(requiredMapping.Key, out var matrixMapping))
                {
                    errors.Add(
                        $"--compat-matrix is missing required mapping from --mapping-catalog: " +
                        $"mode={requiredMapping.Mode}, proxy={requiredMapping.ProxyTypeName}, target={requiredMapping.TargetTypeName}.");
                    continue;
                }

                var actualStatus = matrixMapping.Status ?? string.Empty;
                _ = expectedOutcomes.TryGetExpectedStatuses(requiredMapping.ScenarioId!, out var expectedStatuses);

                if (!expectedStatuses.Contains(actualStatus))
                {
                    errors.Add(
                        $"Required mapping status does not match expected outcomes: " +
                        $"key='{requiredMapping.Key}', scenario='{matrixMapping.Id ?? "(null)"}', " +
                        $"expected=[{string.Join(", ", expectedStatuses)}], actual='{actualStatus}'.");
                }

                if (!string.IsNullOrWhiteSpace(requiredMapping.ScenarioId) &&
                    !string.Equals(matrixMapping.Id, requiredMapping.ScenarioId, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Scenario id mismatch for required mapping '{requiredMapping.Key}'. " +
                        $"Expected='{requiredMapping.ScenarioId}', actual='{matrixMapping.Id ?? "(null)"}'.");
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

        private static bool ValidateScenarioInventory(DuckTypeAotCompatibilityMatrix matrix, string scenarioInventoryPath)
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
            var matrixScenarioIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < matrix.Mappings.Count; i++)
            {
                var mapping = matrix.Mappings[i];
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
                if (!IsScenarioCoveredByMatrix(requiredEntry, matrixScenarioIds))
                {
                    errors.Add($"--compat-matrix is missing required scenario from --scenario-inventory: '{requiredEntry}'.");
                }
            }

            foreach (var matrixScenarioId in matrixScenarioIds)
            {
                if (!IsScenarioTrackedByInventory(matrixScenarioId, inventoryResult.RequiredScenarios))
                {
                    errors.Add(
                        $"--compat-matrix contains scenario id '{matrixScenarioId}' that is not tracked by --scenario-inventory. " +
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

        private static bool IsWildcardScenarioEntry(string entry)
        {
            return entry.Length > 1 && entry[entry.Length - 1] == '*';
        }

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

        private static bool ValidateManifest(DuckTypeAotCompatibilityMatrix matrix, DuckTypeAotManifest manifest)
        {
            var errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(matrix.SchemaVersion) &&
                !string.IsNullOrWhiteSpace(manifest.SchemaVersion) &&
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

        private static bool ValidateManifestAssemblyFingerprints(DuckTypeAotManifest manifest, bool strictAssemblyFingerprintValidation)
        {
            var issues = new List<string>();

            ValidateFingerprints(manifest.TargetAssemblies, "target", issues);
            ValidateFingerprints(manifest.ProxyAssemblies, "proxy", issues);

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

                    if (!string.IsNullOrWhiteSpace(manifest.RegistryAssemblyVersion) &&
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

                    if (!string.IsNullOrWhiteSpace(manifest.RegistryPublicKeyToken))
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

        private static bool ValidateTrimmerDescriptorCoupling(DuckTypeAotCompatibilityMatrix matrix, DuckTypeAotManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(manifest.TrimmerDescriptorPath))
            {
                return true;
            }

            if (!TryReadTrimmerDescriptorRoots(manifest.TrimmerDescriptorPath!, out var rootsByAssembly, out var readError))
            {
                Utils.WriteError(readError);
                return false;
            }

            var errors = new List<string>();

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
                if (!string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(mapping.ProxyAssembly) && !string.IsNullOrWhiteSpace(mapping.ProxyType))
                {
                    ValidateTrimmerDescriptorRoot(
                        rootsByAssembly,
                        mapping.ProxyAssembly!,
                        mapping.ProxyType!,
                        $"compatible mapping '{mapping.Id ?? "(null)"}' proxy root",
                        errors);
                }

                if (!string.IsNullOrWhiteSpace(mapping.TargetAssembly) && !string.IsNullOrWhiteSpace(mapping.TargetType))
                {
                    ValidateTrimmerDescriptorRoot(
                        rootsByAssembly,
                        mapping.TargetAssembly!,
                        mapping.TargetType!,
                        $"compatible mapping '{mapping.Id ?? "(null)"}' target root",
                        errors);
                }

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

            if (manifest.GenericInstantiations is not null)
            {
                foreach (var typeReference in manifest.GenericInstantiations)
                {
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
                    if (string.IsNullOrWhiteSpace(assemblyName))
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
                error = $"Failed to parse trimmer descriptor '{descriptorPath}': {ex.Message}";
                return false;
            }
        }

        private static void ValidateTrimmerDescriptorRoot(
            IReadOnlyDictionary<string, HashSet<string>> rootsByAssembly,
            string assemblyName,
            string typeName,
            string context,
            ICollection<string> errors)
        {
            if (!rootsByAssembly.TryGetValue(assemblyName, out var typeRoots))
            {
                errors.Add($"Trimmer descriptor is missing assembly root '{assemblyName}' required for {context}.");
                return;
            }

            var normalizedTypeName = NormalizeTypeNameForLinker(typeName);
            if (!typeRoots.Contains(normalizedTypeName))
            {
                errors.Add($"Trimmer descriptor is missing type root '{normalizedTypeName}' in assembly '{assemblyName}' required for {context}.");
            }
        }

        private static void ValidateFileFingerprint(string? path, string? expectedSha256, string artifactName, ICollection<string> issues)
        {
            if (string.IsNullOrWhiteSpace(path))
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

        private static bool ValidateChecksum(string? value, out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
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
                if (string.IsNullOrWhiteSpace(assemblyPath))
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
                    if (!string.IsNullOrWhiteSpace(fingerprint.Name) &&
                        !string.Equals(actualName, fingerprint.Name, StringComparison.Ordinal))
                    {
                        issues.Add($"Manifest {assemblyKind} assembly name mismatch for '{resolvedAssemblyPath}'. Expected '{fingerprint.Name}', got '{actualName}'.");
                    }

                    using var module = ModuleDefMD.Load(resolvedAssemblyPath);
                    var actualMvid = module.Mvid?.ToString("D") ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(fingerprint.Mvid) &&
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

        private static string NormalizeTypeNameForLinker(string typeName)
        {
            return typeName.Replace('+', '/');
        }

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

        private readonly struct DuckTypeAotExpectedOutcome
        {
            internal DuckTypeAotExpectedOutcome(string scenarioId, string status)
            {
                ScenarioId = scenarioId;
                Status = status;
            }

            internal string ScenarioId { get; }

            internal string Status { get; }
        }

        private sealed class DuckTypeAotExpectedOutcomes
        {
            private readonly Dictionary<string, HashSet<string>> _expectedStatusesByScenario;

            internal DuckTypeAotExpectedOutcomes(string defaultStatus, IReadOnlyList<DuckTypeAotExpectedOutcome> explicitOutcomes)
            {
                DefaultStatus = defaultStatus;
                ExplicitOutcomes = explicitOutcomes;
                _expectedStatusesByScenario = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                foreach (var expectedOutcome in explicitOutcomes)
                {
                    if (!_expectedStatusesByScenario.TryGetValue(expectedOutcome.ScenarioId, out var statuses))
                    {
                        statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _expectedStatusesByScenario[expectedOutcome.ScenarioId] = statuses;
                    }

                    _ = statuses.Add(expectedOutcome.Status);
                }
            }

            internal static DuckTypeAotExpectedOutcomes DefaultCompatible { get; } =
                new(
                    DuckTypeAotCompatibilityStatuses.Compatible,
                    Array.Empty<DuckTypeAotExpectedOutcome>());

            internal string DefaultStatus { get; }

            internal IReadOnlyList<DuckTypeAotExpectedOutcome> ExplicitOutcomes { get; }

            internal bool TryGetExpectedStatuses(string scenarioId, out IReadOnlyCollection<string> statuses)
            {
                if (_expectedStatusesByScenario.TryGetValue(scenarioId, out var explicitStatuses))
                {
                    statuses = explicitStatuses;
                    return true;
                }

                statuses = new[] { DefaultStatus };
                return false;
            }
        }

        private sealed class DuckTypeAotExpectedOutcomesDocument
        {
            [JsonProperty("schemaVersion")]
            public string? SchemaVersion { get; set; }

            [JsonProperty("defaultStatus")]
            public string? DefaultStatus { get; set; }

            [JsonProperty("expectedOutcomes")]
            public List<DuckTypeAotExpectedOutcomeEntry>? ExpectedOutcomes { get; set; }

            [JsonProperty("outcomes")]
            public List<DuckTypeAotExpectedOutcomeEntry>? Outcomes { get; set; }

            [JsonProperty("expected")]
            public List<DuckTypeAotExpectedOutcomeEntry>? Expected { get; set; }
        }

        private sealed class DuckTypeAotKnownLimitationsDocument
        {
            [JsonProperty("knownLimitations")]
            public List<DuckTypeAotExpectedOutcomeEntry>? KnownLimitations { get; set; }

            [JsonProperty("approvedLimitations")]
            public List<DuckTypeAotExpectedOutcomeEntry>? ApprovedLimitations { get; set; }

            [JsonProperty("approved")]
            public List<DuckTypeAotExpectedOutcomeEntry>? Approved { get; set; }
        }

        private sealed class DuckTypeAotExpectedOutcomeEntry
        {
            [JsonProperty("scenarioId")]
            public string? ScenarioId { get; set; }

            [JsonProperty("status")]
            public string? Status { get; set; }
        }
    }
}
