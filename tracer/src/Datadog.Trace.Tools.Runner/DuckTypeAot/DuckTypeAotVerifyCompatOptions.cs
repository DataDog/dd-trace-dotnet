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
        /// <param name="mappingCatalogPath">The mapping catalog path value.</param>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="scenarioInventoryPath">The scenario inventory path value.</param>
        /// <param name="expectedOutcomesPath">The expected outcomes path value.</param>
        /// <param name="knownLimitationsPath">The known limitations path value.</param>
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
        /// Gets mapping catalog path.
        /// </summary>
        /// <value>The mapping catalog path value.</value>
        public string? MappingCatalogPath { get; }

        /// <summary>
        /// Gets manifest path.
        /// </summary>
        /// <value>The manifest path value.</value>
        public string? ManifestPath { get; }

        /// <summary>
        /// Gets scenario inventory path.
        /// </summary>
        /// <value>The scenario inventory path value.</value>
        public string? ScenarioInventoryPath { get; }

        /// <summary>
        /// Gets expected outcomes path.
        /// </summary>
        /// <value>The expected outcomes path value.</value>
        public string? ExpectedOutcomesPath { get; }

        /// <summary>
        /// Gets known limitations path.
        /// </summary>
        /// <value>The known limitations path value.</value>
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
