// <copyright file="DuckTypeAotVerifyCompatOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal enum DuckTypeAotFailureMode
    {
        Default = 0,
        Strict = 1
    }

    internal sealed class DuckTypeAotVerifyCompatOptions
    {
        public DuckTypeAotVerifyCompatOptions(
            string compatReportPath,
            string compatMatrixPath,
            string? mappingCatalogPath,
            string? manifestPath,
            string? scenarioInventoryPath = null,
            string? expectedOutcomesPath = null,
            string? knownLimitationsPath = null,
            bool strictAssemblyFingerprintValidation = false,
            DuckTypeAotFailureMode failureMode = DuckTypeAotFailureMode.Default)
        {
            CompatReportPath = compatReportPath;
            CompatMatrixPath = compatMatrixPath;
            MappingCatalogPath = mappingCatalogPath;
            ManifestPath = manifestPath;
            ScenarioInventoryPath = scenarioInventoryPath;
            ExpectedOutcomesPath = expectedOutcomesPath;
            KnownLimitationsPath = knownLimitationsPath;
            FailureMode = failureMode;
            StrictAssemblyFingerprintValidation = strictAssemblyFingerprintValidation || failureMode == DuckTypeAotFailureMode.Strict;
        }

        public string CompatReportPath { get; }

        public string CompatMatrixPath { get; }

        public string? MappingCatalogPath { get; }

        public string? ManifestPath { get; }

        public string? ScenarioInventoryPath { get; }

        public string? ExpectedOutcomesPath { get; }

        public string? KnownLimitationsPath { get; }

        public DuckTypeAotFailureMode FailureMode { get; }

        public bool StrictAssemblyFingerprintValidation { get; }
    }
}
