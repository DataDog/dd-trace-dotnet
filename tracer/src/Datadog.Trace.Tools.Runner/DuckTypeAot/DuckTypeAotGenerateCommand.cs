// <copyright file="DuckTypeAotGenerateCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Represents duck type aot generate command.
    /// </summary>
    internal class DuckTypeAotGenerateCommand : CommandWithExamples
    {
        private readonly Option<string[]> _proxyAssemblyOption = new("--proxy-assembly", "Proxy definition assembly path. Can be provided multiple times.")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };

        private readonly Option<string[]> _targetAssemblyOption = new("--target-assembly", "Target assembly path. Can be provided multiple times.")
        {
            AllowMultipleArgumentsPerToken = true
        };

        private readonly Option<string[]> _targetFolderOption = new("--target-folder", "Folder containing target assemblies. Can be provided multiple times.")
        {
            AllowMultipleArgumentsPerToken = true
        };

        private readonly Option<string[]> _targetFilterOption = new("--target-filter", getDefaultValue: () => new[] { "*.dll" }, "Target assembly search glob. Defaults to *.dll.")
        {
            AllowMultipleArgumentsPerToken = true
        };

        /// <summary>
        /// Stores cached map file option data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private readonly Option<string?> _mapFileOption = new("--map-file", "Optional JSON map file with explicit mapping entries and overrides.");

        /// <summary>
        /// Stores cached mapping catalog option data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private readonly Option<string?> _mappingCatalogOption = new("--mapping-catalog", "Optional declared mapping inventory for CI/release coverage enforcement.");

        /// <summary>
        /// Stores cached require mapping catalog option data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private readonly Option<bool> _requireMappingCatalogOption = new("--require-mapping-catalog", "Require --mapping-catalog and fail if it is missing or empty.");

        /// <summary>
        /// Stores generic instantiations option.
        /// </summary>
        private readonly Option<string?> _genericInstantiationsOption = new("--generic-instantiations", "Optional closed-generic roots file.");

        /// <summary>
        /// Stores assembly name option.
        /// </summary>
        private readonly Option<string?> _assemblyNameOption = new("--assembly-name", "Optional generated assembly name.");

        /// <summary>
        /// Stores emit trimmer descriptor option.
        /// </summary>
        private readonly Option<string?> _emitTrimmerDescriptorOption = new("--emit-trimmer-descriptor", "Optional linker descriptor output path. Defaults to <output>.linker.xml.");

        /// <summary>
        /// Stores emit props option.
        /// </summary>
        private readonly Option<string?> _emitPropsOption = new("--emit-props", "Optional MSBuild props output path. Defaults to <output>.props.");

        /// <summary>
        /// Stores strong name key file option.
        /// </summary>
        private readonly Option<string?> _strongNameKeyFileOption = new("--strong-name-key-file", "Optional strong-name key (.snk) file used to sign the generated registry assembly.");
        private readonly Option<string> _outputOption = new("--output", "Generated AOT registry assembly output path.")
        {
            IsRequired = true
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotGenerateCommand"/> class.
        /// </summary>
        public DuckTypeAotGenerateCommand()
            : base("generate", "Generate NativeAOT DuckTyping registry artifacts")
        {
            AddOption(_proxyAssemblyOption);
            AddOption(_targetAssemblyOption);
            AddOption(_targetFolderOption);
            AddOption(_targetFilterOption);
            AddOption(_mapFileOption);
            AddOption(_mappingCatalogOption);
            AddOption(_requireMappingCatalogOption);
            AddOption(_genericInstantiationsOption);
            AddOption(_assemblyNameOption);
            AddOption(_emitTrimmerDescriptorOption);
            AddOption(_emitPropsOption);
            AddOption(_strongNameKeyFileOption);
            AddOption(_outputOption);

            AddExample("dd-trace ducktype-aot generate --proxy-assembly My.Proxy.dll --target-assembly ThirdParty.dll --output Datadog.Trace.DuckType.AotRegistry.dll");
            AddExample("dd-trace ducktype-aot generate --proxy-assembly My.Proxy.dll --target-folder ./bin --target-filter *.dll --output Datadog.Trace.DuckType.AotRegistry.dll");
            AddExample("dd-trace ducktype-aot generate --proxy-assembly My.Proxy.dll --target-assembly ThirdParty.dll --mapping-catalog ducktype-aot-catalog.json --require-mapping-catalog --output Datadog.Trace.DuckType.AotRegistry.dll");

            this.SetHandler(Execute);
        }

        /// <summary>
        /// Executes execute.
        /// </summary>
        /// <param name="context">The context value.</param>
        private void Execute(InvocationContext context)
        {
            var proxyAssemblies = _proxyAssemblyOption.GetValue(context) ?? Array.Empty<string>();
            var targetAssemblies = _targetAssemblyOption.GetValue(context) ?? Array.Empty<string>();
            var targetFolders = _targetFolderOption.GetValue(context) ?? Array.Empty<string>();
            var targetFilters = _targetFilterOption.GetValue(context) ?? new[] { "*.dll" };

            var outputPath = _outputOption.GetValue(context);
            var mapFile = _mapFileOption.GetValue(context);
            var mappingCatalog = _mappingCatalogOption.GetValue(context);
            var requireMappingCatalog = _requireMappingCatalogOption.GetValue(context);
            var genericInstantiations = _genericInstantiationsOption.GetValue(context);
            var assemblyName = _assemblyNameOption.GetValue(context);
            var trimmerDescriptorPath = _emitTrimmerDescriptorOption.GetValue(context) ?? $"{outputPath}.linker.xml";
            var propsPath = _emitPropsOption.GetValue(context) ?? $"{outputPath}.props";
            var strongNameKeyFile = _strongNameKeyFileOption.GetValue(context);

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),
                targetAssemblies.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),
                targetFolders.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),
                targetFilters.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),
                mapFile,
                mappingCatalog,
                genericInstantiations,
                outputPath,
                assemblyName,
                trimmerDescriptorPath,
                propsPath,
                requireMappingCatalog,
                strongNameKeyFile);

            context.ExitCode = DuckTypeAotGenerateProcessor.Process(options);
        }
    }
}
