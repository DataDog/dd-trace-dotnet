// <copyright file="DuckTypeAotVerifyCompatOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Defines named constants for duck type aot failure mode.
    /// </summary>
    internal enum DuckTypeAotFailureMode
    {
        /// <summary>
        /// Represents default.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Represents strict.
        /// </summary>
        Strict = 1
    }

    /// <summary>
    /// Represents duck type aot verify compat options.
    /// </summary>
    internal sealed class DuckTypeAotVerifyCompatOptions
    {
        private DuckTypeAotVerifyCompatOptions(
            string compatReportPath,
            string compatMatrixPath,
            string mapFilePath,
            string? mappingCatalogPath,
            string? manifestPath,
            string? scenarioInventoryPath,
            string? expectedOutcomesPath,
            string? knownLimitationsPath,
            bool strictAssemblyFingerprintValidation,
            DuckTypeAotFailureMode failureMode)
        {
            CompatReportPath = compatReportPath;
            CompatMatrixPath = compatMatrixPath;
            MapFilePath = mapFilePath;
            ManifestPath = manifestPath;
            MappingCatalogPath = mappingCatalogPath;
            ScenarioInventoryPath = scenarioInventoryPath;
            ExpectedOutcomesPath = expectedOutcomesPath;
            KnownLimitationsPath = knownLimitationsPath;
            FailureMode = failureMode;
            StrictAssemblyFingerprintValidation = strictAssemblyFingerprintValidation || failureMode == DuckTypeAotFailureMode.Strict;
        }

        /// <summary>
        /// Gets compat report path.
        /// </summary>
        /// <value>The compat report path value.</value>
        public string CompatReportPath { get; }

        /// <summary>
        /// Gets compat matrix path.
        /// </summary>
        /// <value>The compat matrix path value.</value>
        public string CompatMatrixPath { get; }

        /// <summary>
        /// Gets map file path.
        /// </summary>
        /// <value>The map file path value.</value>
        public string MapFilePath { get; }

        /// <summary>
        /// Gets manifest path.
        /// </summary>
        /// <value>The manifest path value.</value>
        public string? ManifestPath { get; }

        /// <summary>
        /// Gets mapping catalog path.
        /// </summary>
        public string? MappingCatalogPath { get; }

        /// <summary>
        /// Gets scenario inventory path.
        /// </summary>
        public string? ScenarioInventoryPath { get; }

        /// <summary>
        /// Gets the legacy expected outcomes path retained for migration and focused compatibility tests.
        /// </summary>
        public string? ExpectedOutcomesPath { get; }

        /// <summary>
        /// Gets the legacy known limitations path retained for migration and focused compatibility tests.
        /// </summary>
        public string? KnownLimitationsPath { get; }

        /// <summary>
        /// Gets failure mode.
        /// </summary>
        /// <value>The failure mode value.</value>
        public DuckTypeAotFailureMode FailureMode { get; }

        /// <summary>
        /// Gets a value indicating whether strict assembly fingerprint validation.
        /// </summary>
        /// <value>The strict assembly fingerprint validation value.</value>
        public bool StrictAssemblyFingerprintValidation { get; }

        /// <summary>
        /// Creates options for the canonical map-file compatibility contract.
        /// </summary>
        /// <param name="compatReportPath">The compat report path value.</param>
        /// <param name="compatMatrixPath">The compat matrix path value.</param>
        /// <param name="mapFilePath">The canonical map file path value.</param>
        /// <param name="mappingCatalogPath">The mapping catalog path value.</param>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="scenarioInventoryPath">The scenario inventory path value.</param>
        /// <param name="strictAssemblyFingerprintValidation">The strict assembly fingerprint validation value.</param>
        /// <param name="failureMode">The failure mode value.</param>
        public static DuckTypeAotVerifyCompatOptions CreateCanonicalMapContract(
            string compatReportPath,
            string compatMatrixPath,
            string mapFilePath,
            string? mappingCatalogPath = null,
            string? manifestPath = null,
            string? scenarioInventoryPath = null,
            bool strictAssemblyFingerprintValidation = false,
            DuckTypeAotFailureMode failureMode = DuckTypeAotFailureMode.Default)
            => new(
                compatReportPath,
                compatMatrixPath,
                mapFilePath,
                mappingCatalogPath,
                manifestPath,
                scenarioInventoryPath,
                expectedOutcomesPath: null,
                knownLimitationsPath: null,
                strictAssemblyFingerprintValidation,
                failureMode);

        /// <summary>
        /// Creates options for the legacy compatibility contract retained for migration and focused compatibility tests.
        /// </summary>
        /// <param name="compatReportPath">The compat report path value.</param>
        /// <param name="compatMatrixPath">The compat matrix path value.</param>
        /// <param name="mappingCatalogPath">The mapping catalog path value.</param>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="scenarioInventoryPath">The scenario inventory path value.</param>
        /// <param name="expectedOutcomesPath">Legacy expected outcomes path retained for migration and focused compatibility tests.</param>
        /// <param name="knownLimitationsPath">Legacy known limitations path retained for migration and focused compatibility tests.</param>
        /// <param name="strictAssemblyFingerprintValidation">The strict assembly fingerprint validation value.</param>
        /// <param name="failureMode">The failure mode value.</param>
        public static DuckTypeAotVerifyCompatOptions CreateLegacyContract(
            string compatReportPath,
            string compatMatrixPath,
            string? mappingCatalogPath = null,
            string? manifestPath = null,
            string? scenarioInventoryPath = null,
            string? expectedOutcomesPath = null,
            string? knownLimitationsPath = null,
            bool strictAssemblyFingerprintValidation = false,
            DuckTypeAotFailureMode failureMode = DuckTypeAotFailureMode.Default)
            => new(
                compatReportPath,
                compatMatrixPath,
                mapFilePath: string.Empty,
                mappingCatalogPath,
                manifestPath,
                scenarioInventoryPath,
                expectedOutcomesPath,
                knownLimitationsPath,
                strictAssemblyFingerprintValidation,
                failureMode);
    }
}
