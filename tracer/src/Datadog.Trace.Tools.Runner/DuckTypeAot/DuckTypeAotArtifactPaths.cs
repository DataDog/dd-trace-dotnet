// <copyright file="DuckTypeAotArtifactPaths.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Represents duck type aot artifact paths.
    /// </summary>
    internal sealed class DuckTypeAotArtifactPaths
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotArtifactPaths"/> class.
        /// </summary>
        /// <param name="outputAssemblyPath">The output assembly path value.</param>
        /// <param name="manifestPath">The manifest path value.</param>
        /// <param name="compatibilityMatrixPath">The compatibility matrix path value.</param>
        /// <param name="compatibilityReportPath">The compatibility report path value.</param>
        /// <param name="trimmerDescriptorPath">The trimmer descriptor path value.</param>
        /// <param name="propsPath">The props path value.</param>
        public DuckTypeAotArtifactPaths(
            string outputAssemblyPath,
            string manifestPath,
            string compatibilityMatrixPath,
            string compatibilityReportPath,
            string trimmerDescriptorPath,
            string propsPath)
        {
            OutputAssemblyPath = outputAssemblyPath;
            ManifestPath = manifestPath;
            CompatibilityMatrixPath = compatibilityMatrixPath;
            CompatibilityReportPath = compatibilityReportPath;
            TrimmerDescriptorPath = trimmerDescriptorPath;
            PropsPath = propsPath;
        }

        /// <summary>
        /// Gets output assembly path.
        /// </summary>
        /// <value>The output assembly path value.</value>
        public string OutputAssemblyPath { get; }

        /// <summary>
        /// Gets manifest path.
        /// </summary>
        /// <value>The manifest path value.</value>
        public string ManifestPath { get; }

        /// <summary>
        /// Gets compatibility matrix path.
        /// </summary>
        /// <value>The compatibility matrix path value.</value>
        public string CompatibilityMatrixPath { get; }

        /// <summary>
        /// Gets compatibility report path.
        /// </summary>
        /// <value>The compatibility report path value.</value>
        public string CompatibilityReportPath { get; }

        /// <summary>
        /// Gets trimmer descriptor path.
        /// </summary>
        /// <value>The trimmer descriptor path value.</value>
        public string TrimmerDescriptorPath { get; }

        /// <summary>
        /// Gets props path.
        /// </summary>
        /// <value>The props path value.</value>
        public string PropsPath { get; }

        /// <summary>
        /// Creates create.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The result produced by this operation.</returns>
        public static DuckTypeAotArtifactPaths Create(DuckTypeAotGenerateOptions options)
        {
            var outputAssemblyPath = Path.GetFullPath(options.OutputPath);
            var manifestPath = outputAssemblyPath + ".manifest.json";
            var compatibilityMatrixPath = outputAssemblyPath + ".compat.json";
            var compatibilityReportPath = outputAssemblyPath + ".compat.md";
            var trimmerDescriptorPath = Path.GetFullPath(options.TrimmerDescriptorPath);
            var propsPath = Path.GetFullPath(options.PropsPath);

            return new DuckTypeAotArtifactPaths(
                outputAssemblyPath,
                manifestPath,
                compatibilityMatrixPath,
                compatibilityReportPath,
                trimmerDescriptorPath,
                propsPath);
        }
    }
}
