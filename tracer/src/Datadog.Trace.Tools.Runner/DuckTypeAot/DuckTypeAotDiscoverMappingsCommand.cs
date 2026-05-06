// <copyright file="DuckTypeAotDiscoverMappingsCommand.cs" company="Datadog">
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
    /// Discovers canonical DuckType AOT mappings from type-level attributes.
    /// </summary>
    internal class DuckTypeAotDiscoverMappingsCommand : CommandWithExamples
    {
        private readonly Option<string[]> _proxyAssemblyOption = new("--proxy-assembly", "Proxy definition assembly path. Can be provided multiple times.")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };

        private readonly Option<string[]> _targetFolderOption = new("--target-folder", "Folder containing target assemblies. Can be provided multiple times.")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };

        private readonly Option<string[]> _targetFilterOption = new("--target-filter", getDefaultValue: () => new[] { "*.dll" }, "Target assembly search glob. Defaults to *.dll.")
        {
            AllowMultipleArgumentsPerToken = true
        };

        private readonly Option<string> _outputOption = new("--output", "Canonical map file output path.")
        {
            IsRequired = true
        };

        private readonly Option<string?> _warningsReportOption = new("--warnings-report", "Optional diagnostics output path (JSON).");
        private readonly Option<bool> _strictOption = new("--strict", "Fail when at least one discovered mapping is dropped.");

        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotDiscoverMappingsCommand"/> class.
        /// </summary>
        public DuckTypeAotDiscoverMappingsCommand()
            : base("discover-mappings", "Discover mappings from type-level attributes and emit a canonical map file")
        {
            AddOption(_proxyAssemblyOption);
            AddOption(_targetFolderOption);
            AddOption(_targetFilterOption);
            AddOption(_outputOption);
            AddOption(_warningsReportOption);
            AddOption(_strictOption);

            AddExample("dd-trace ducktype-aot discover-mappings --proxy-assembly Datadog.Trace.DuckTyping.Tests.dll --target-folder ./bin --target-filter *.dll --output ducktype-aot-bible-mappings.json");
            AddExample("dd-trace ducktype-aot discover-mappings --proxy-assembly Contracts.dll --target-folder ./bin --target-filter *.dll --output ducktype-aot-mappings.json --warnings-report ducktype-aot-discover-warnings.json");

            this.SetHandler(Execute);
        }

        private void Execute(InvocationContext context)
        {
            var proxyAssemblies = _proxyAssemblyOption.GetValue(context) ?? Array.Empty<string>();
            var targetFolders = _targetFolderOption.GetValue(context) ?? Array.Empty<string>();
            var targetFilters = _targetFilterOption.GetValue(context) ?? new[] { "*.dll" };
            var output = _outputOption.GetValue(context);
            var warningsReport = _warningsReportOption.GetValue(context);
            var strict = _strictOption.GetValue(context);

            var options = new DuckTypeAotDiscoverMappingsOptions(
                proxyAssemblies.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),
                targetFolders.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),
                targetFilters.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),
                output,
                warningsReport,
                strict);

            context.ExitCode = DuckTypeAotDiscoverMappingsProcessor.Process(options);
        }
    }
}
