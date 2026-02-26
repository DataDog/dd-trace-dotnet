// <copyright file="DuckTypeAotVerifyCompatProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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

            var incompatibleMappings = matrix.Mappings
                                             .Where(mapping => !string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, System.StringComparison.OrdinalIgnoreCase))
                                             .ToList();

            if (incompatibleMappings.Count > 0)
            {
                Utils.WriteError($"Compatibility verification failed. Non-compatible mappings found: {incompatibleMappings.Count}.");
                return 1;
            }

            return 0;
        }
    }
}
