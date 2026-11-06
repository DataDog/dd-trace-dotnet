// <copyright file="DuckTypeAotDiscoverMappingsOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Represents options for <c>ducktype-aot discover-mappings</c>.
    /// </summary>
    internal sealed class DuckTypeAotDiscoverMappingsOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotDiscoverMappingsOptions"/> class.
        /// </summary>
        /// <param name="proxyAssemblies">Proxy definition assembly paths.</param>
        /// <param name="targetFolders">Target assembly search folders.</param>
        /// <param name="targetFilters">Target assembly file globs.</param>
        /// <param name="outputPath">Canonical map output file path.</param>
        /// <param name="warningsReportPath">Optional warnings/diagnostics output path.</param>
        /// <param name="strict">Whether to fail when any discovered mapping is dropped.</param>
        public DuckTypeAotDiscoverMappingsOptions(
            IReadOnlyList<string> proxyAssemblies,
            IReadOnlyList<string> targetFolders,
            IReadOnlyList<string> targetFilters,
            string outputPath,
            string? warningsReportPath,
            bool strict)
        {
            ProxyAssemblies = proxyAssemblies;
            TargetFolders = targetFolders;
            TargetFilters = targetFilters;
            OutputPath = outputPath;
            WarningsReportPath = warningsReportPath;
            Strict = strict;
        }

        /// <summary>
        /// Gets proxy assemblies.
        /// </summary>
        public IReadOnlyList<string> ProxyAssemblies { get; }

        /// <summary>
        /// Gets target folders.
        /// </summary>
        public IReadOnlyList<string> TargetFolders { get; }

        /// <summary>
        /// Gets target filters.
        /// </summary>
        public IReadOnlyList<string> TargetFilters { get; }

        /// <summary>
        /// Gets output path.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Gets warnings report path.
        /// </summary>
        public string? WarningsReportPath { get; }

        /// <summary>
        /// Gets a value indicating whether strict mode is enabled.
        /// </summary>
        public bool Strict { get; }
    }
}
