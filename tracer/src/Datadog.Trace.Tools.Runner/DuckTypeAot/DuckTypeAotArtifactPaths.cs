// <copyright file="DuckTypeAotArtifactPaths.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal sealed class DuckTypeAotArtifactPaths
    {
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

        public string OutputAssemblyPath { get; }

        public string ManifestPath { get; }

        public string CompatibilityMatrixPath { get; }

        public string CompatibilityReportPath { get; }

        public string TrimmerDescriptorPath { get; }

        public string PropsPath { get; }

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
