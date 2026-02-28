// <copyright file="DuckTypeAotGenerateOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Represents duck type aot generate options.
    /// </summary>
    internal sealed class DuckTypeAotGenerateOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotGenerateOptions"/> class.
        /// </summary>
        /// <param name="proxyAssemblies">The proxy assemblies value.</param>
        /// <param name="targetAssemblies">The target assemblies value.</param>
        /// <param name="targetFolders">The target folders value.</param>
        /// <param name="targetFilters">The target filters value.</param>
        /// <param name="mapFile">The map file value.</param>
        /// <param name="mappingCatalog">The mapping catalog value.</param>
        /// <param name="genericInstantiationsFile">The generic instantiations file value.</param>
        /// <param name="outputPath">The output path value.</param>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <param name="trimmerDescriptorPath">The trimmer descriptor path value.</param>
        /// <param name="propsPath">The props path value.</param>
        /// <param name="requireMappingCatalog">The require mapping catalog value.</param>
        /// <param name="strongNameKeyFile">The strong name key file value.</param>
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

        /// <summary>
        /// Gets proxy assemblies.
        /// </summary>
        /// <value>The proxy assemblies value.</value>
        public IReadOnlyList<string> ProxyAssemblies { get; }

        /// <summary>
        /// Gets target assemblies.
        /// </summary>
        /// <value>The target assemblies value.</value>
        public IReadOnlyList<string> TargetAssemblies { get; }

        /// <summary>
        /// Gets target folders.
        /// </summary>
        /// <value>The target folders value.</value>
        public IReadOnlyList<string> TargetFolders { get; }

        /// <summary>
        /// Gets target filters.
        /// </summary>
        /// <value>The target filters value.</value>
        public IReadOnlyList<string> TargetFilters { get; }

        /// <summary>
        /// Gets map file.
        /// </summary>
        /// <value>The map file value.</value>
        public string? MapFile { get; }

        /// <summary>
        /// Gets mapping catalog.
        /// </summary>
        /// <value>The mapping catalog value.</value>
        public string? MappingCatalog { get; }

        /// <summary>
        /// Gets generic instantiations file.
        /// </summary>
        /// <value>The generic instantiations file value.</value>
        public string? GenericInstantiationsFile { get; }

        /// <summary>
        /// Gets output path.
        /// </summary>
        /// <value>The output path value.</value>
        public string OutputPath { get; }

        /// <summary>
        /// Gets assembly name.
        /// </summary>
        /// <value>The assembly name value.</value>
        public string? AssemblyName { get; }

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
        /// Gets a value indicating whether require mapping catalog.
        /// </summary>
        /// <value>The require mapping catalog value.</value>
        public bool RequireMappingCatalog { get; }

        /// <summary>
        /// Gets strong name key file.
        /// </summary>
        /// <value>The strong name key file value.</value>
        public string? StrongNameKeyFile { get; }
    }
}
