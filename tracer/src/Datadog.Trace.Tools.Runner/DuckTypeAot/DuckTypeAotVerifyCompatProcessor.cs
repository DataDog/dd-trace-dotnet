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

            var duplicateMappingIds = matrix.Mappings
                                            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Id))
                                            .GroupBy(mapping => mapping.Id!, StringComparer.Ordinal)
                                            .Where(group => group.Count() > 1)
                                            .Select(group => group.Key)
                                            .OrderBy(id => id, StringComparer.Ordinal)
                                            .ToList();
            if (duplicateMappingIds.Count > 0)
            {
                Utils.WriteError($"--compat-matrix contains duplicate mapping ids: {string.Join(", ", duplicateMappingIds)}");
                return 1;
            }

            var incompatibleMappings = matrix.Mappings
                                             .Where(mapping => !string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, System.StringComparison.OrdinalIgnoreCase))
                                             .ToList();

            if (incompatibleMappings.Count > 0)
            {
                Utils.WriteError($"Compatibility verification failed. Non-compatible mappings found: {incompatibleMappings.Count}.");
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
            }

            if (!string.IsNullOrWhiteSpace(options.MappingCatalogPath))
            {
                if (!ValidateMappingCatalog(matrix, options.MappingCatalogPath!))
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

        private static bool ValidateMappingCatalog(DuckTypeAotCompatibilityMatrix matrix, string mappingCatalogPath)
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

                if (!string.Equals(matrixMapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Required mapping is not compatible in --compat-matrix: key='{requiredMapping.Key}', status='{matrixMapping.Status ?? "(null)"}'.");
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
    }
}
