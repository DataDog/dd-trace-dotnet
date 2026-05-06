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
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotVerifyCompatOptions"/> class.
        /// </summary>
        /// <param name="compatReportPath">The compat report path value.</param>
        /// <param name="compatMatrixPath">The compat matrix path value.</param>
        /// <param name="mapFilePath">The canonical map file path value.</param>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="strictAssemblyFingerprintValidation">The strict assembly fingerprint validation value.</param>
        /// <param name="failureMode">The failure mode value.</param>
        public DuckTypeAotVerifyCompatOptions(
            string compatReportPath,
            string compatMatrixPath,
            string mapFilePath,
            string? manifestPath,
            bool strictAssemblyFingerprintValidation = false,
            DuckTypeAotFailureMode failureMode = DuckTypeAotFailureMode.Default)
        {
            CompatReportPath = compatReportPath;
            CompatMatrixPath = compatMatrixPath;
            MapFilePath = mapFilePath;
            ManifestPath = manifestPath;
            MappingCatalogPath = null;
            ScenarioInventoryPath = null;
            ExpectedOutcomesPath = null;
            KnownLimitationsPath = null;
            FailureMode = failureMode;
            StrictAssemblyFingerprintValidation = strictAssemblyFingerprintValidation || failureMode == DuckTypeAotFailureMode.Strict;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotVerifyCompatOptions"/> class using legacy parameters.
        /// </summary>
        /// <param name="compatReportPath">The compat report path value.</param>
        /// <param name="compatMatrixPath">The compat matrix path value.</param>
        /// <param name="mappingCatalogPath">Legacy mapping catalog path. Retained for source compatibility and ignored.</param>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="scenarioInventoryPath">Legacy scenario inventory path. Retained for source compatibility and ignored.</param>
        /// <param name="expectedOutcomesPath">Legacy expected outcomes path. Retained for source compatibility and ignored.</param>
        /// <param name="knownLimitationsPath">Legacy known limitations path. Retained for source compatibility and ignored.</param>
        /// <param name="strictAssemblyFingerprintValidation">The strict assembly fingerprint validation value.</param>
        /// <param name="failureMode">The failure mode value.</param>
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
            : this(
                compatReportPath,
                compatMatrixPath,
                mappingCatalogPath ?? string.Empty,
                manifestPath,
                strictAssemblyFingerprintValidation,
                failureMode)
        {
            MappingCatalogPath = mappingCatalogPath;
            ScenarioInventoryPath = scenarioInventoryPath;
            ExpectedOutcomesPath = expectedOutcomesPath;
            KnownLimitationsPath = knownLimitationsPath;
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
        /// Gets legacy mapping catalog path.
        /// </summary>
        public string? MappingCatalogPath { get; }

        /// <summary>
        /// Gets legacy scenario inventory path.
        /// </summary>
        public string? ScenarioInventoryPath { get; }

        /// <summary>
        /// Gets legacy expected outcomes path.
        /// </summary>
        public string? ExpectedOutcomesPath { get; }

        /// <summary>
        /// Gets legacy known limitations path.
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
    }
}
