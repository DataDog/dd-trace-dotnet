// <copyright file="DuckTypeAotGenerateOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal sealed class DuckTypeAotGenerateOptions
    {
        public DuckTypeAotGenerateOptions(
            IReadOnlyList<string> proxyAssemblies,
            IReadOnlyList<string> targetAssemblies,
            IReadOnlyList<string> targetFolders,
            IReadOnlyList<string> targetFilters,
            string? mapFile,
            string? mappingCatalog,
            string? genericInstantiationsFile,
            string outputPath,
            string? assemblyName,
            string trimmerDescriptorPath,
            string propsPath,
            bool requireMappingCatalog = false,
            string? strongNameKeyFile = null)
        {
            ProxyAssemblies = proxyAssemblies;
            TargetAssemblies = targetAssemblies;
            TargetFolders = targetFolders;
            TargetFilters = targetFilters;
            MapFile = mapFile;
            MappingCatalog = mappingCatalog;
            GenericInstantiationsFile = genericInstantiationsFile;
            OutputPath = outputPath;
            AssemblyName = assemblyName;
            TrimmerDescriptorPath = trimmerDescriptorPath;
            PropsPath = propsPath;
            RequireMappingCatalog = requireMappingCatalog;
            StrongNameKeyFile = strongNameKeyFile;
        }

        public IReadOnlyList<string> ProxyAssemblies { get; }

        public IReadOnlyList<string> TargetAssemblies { get; }

        public IReadOnlyList<string> TargetFolders { get; }

        public IReadOnlyList<string> TargetFilters { get; }

        public string? MapFile { get; }

        public string? MappingCatalog { get; }

        public string? GenericInstantiationsFile { get; }

        public string OutputPath { get; }

        public string? AssemblyName { get; }

        public string TrimmerDescriptorPath { get; }

        public string PropsPath { get; }

        public bool RequireMappingCatalog { get; }

        public string? StrongNameKeyFile { get; }
    }
}
