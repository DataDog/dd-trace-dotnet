// <copyright file="DuckTypeAotVerifyCompatProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

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

            if (!string.IsNullOrWhiteSpace(options.MappingCatalogPath))
            {
                if (!ValidateMappingCatalog(matrix, options.MappingCatalogPath!))
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

            key = new DuckTypeAotMapping(
                    mapping.ProxyType,
                    mapping.ProxyAssembly,
                    mapping.TargetType,
                    mapping.TargetAssembly,
                    mode,
                    DuckTypeAotMappingSource.MapFile)
                .Key;
            return true;
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
