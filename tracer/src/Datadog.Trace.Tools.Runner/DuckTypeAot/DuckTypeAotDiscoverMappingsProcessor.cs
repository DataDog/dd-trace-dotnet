// <copyright file="DuckTypeAotDiscoverMappingsProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Implements <c>ducktype-aot discover-mappings</c>.
    /// </summary>
    internal static class DuckTypeAotDiscoverMappingsProcessor
    {
        /// <summary>
        /// Executes mapping discovery and writes a canonical map file with generation-compatible mappings only.
        /// </summary>
        /// <param name="options">The command options.</param>
        /// <returns>0 on success; otherwise 1.</returns>
        internal static int Process(DuckTypeAotDiscoverMappingsOptions options)
        {
            var errors = Validate(options);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Utils.WriteError(error);
                }

                return 1;
            }

            var discoveryResult = DuckTypeAotAttributeDiscovery.Discover(options.ProxyAssemblies);
            foreach (var warning in discoveryResult.Warnings)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning.EscapeMarkup()}");
            }

            if (discoveryResult.Errors.Count > 0)
            {
                foreach (var error in discoveryResult.Errors)
                {
                    Utils.WriteError(error);
                }

                return 1;
            }

            if (discoveryResult.Mappings.Count == 0)
            {
                Utils.WriteError("No mappings were discovered from type-level attributes.");
                return 1;
            }

            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "ducktype-aot-discover-mappings", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                var discoveredMapPath = Path.Combine(temporaryDirectory, "discovered.map.json");
                WriteCanonicalMap(discoveryResult.Mappings, discoveredMapPath);

                var generateOptions = new DuckTypeAotGenerateOptions(
                    options.ProxyAssemblies,
                    Array.Empty<string>(),
                    options.TargetFolders,
                    options.TargetFilters,
                    discoveredMapPath,
                    genericInstantiationsFile: null,
                    outputPath: Path.Combine(temporaryDirectory, "Datadog.Trace.DuckType.AotRegistry.Discover.dll"),
                    assemblyName: "Datadog.Trace.DuckType.AotRegistry.Discover",
                    trimmerDescriptorPath: Path.Combine(temporaryDirectory, "discover.linker.xml"),
                    propsPath: Path.Combine(temporaryDirectory, "discover.props"),
                    strongNameKeyFile: null);

                var mappingResolution = DuckTypeAotMappingResolver.Resolve(generateOptions);
                var warnings = new List<string>();
                warnings.AddRange(discoveryResult.Warnings);
                warnings.AddRange(mappingResolution.Warnings);
                warnings.AddRange(mappingResolution.Errors.Select(error => $"Dropped mapping input: {error}"));

                DuckTypeAotRegistryEmissionResult emissionResult;
                try
                {
                    var artifactPaths = DuckTypeAotArtifactPaths.Create(generateOptions);
                    emissionResult = DuckTypeAotRegistryAssemblyEmitter.Emit(generateOptions, artifactPaths, mappingResolution);
                }
                catch (Exception ex)
                {
                    Utils.WriteError($"Failed to evaluate mapping compatibility during discovery: {ex.Message}");
                    return 1;
                }

                var compatibleMappings = new List<DuckTypeAotMapping>();
                var droppedMappings = new List<DiscoverDroppedMapping>();

                foreach (var mapping in mappingResolution.Mappings.OrderBy(m => m.Key, StringComparer.Ordinal))
                {
                    if (!emissionResult.MappingResultsByKey.TryGetValue(mapping.Key, out var result))
                    {
                        droppedMappings.Add(
                            new DiscoverDroppedMapping(
                                mapping,
                                status: "unknown",
                                diagnosticCode: null,
                                details: "Compatibility result was not produced."));
                        continue;
                    }

                    if (string.Equals(result.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.OrdinalIgnoreCase))
                    {
                        compatibleMappings.Add(mapping);
                    }
                    else
                    {
                        droppedMappings.Add(
                            new DiscoverDroppedMapping(
                                mapping,
                                result.Status,
                                result.DiagnosticCode,
                                result.Detail));
                    }
                }

                if (compatibleMappings.Count == 0)
                {
                    Utils.WriteError("Discovery finished, but no generation-compatible mappings were found.");
                    WriteDiagnosticsReport(options.WarningsReportPath, discoveryResult.Mappings.Count, compatibleMappings, droppedMappings, warnings);
                    return 1;
                }

                WriteCanonicalMap(compatibleMappings, options.OutputPath);
                WriteDiagnosticsReport(options.WarningsReportPath, discoveryResult.Mappings.Count, compatibleMappings, droppedMappings, warnings);

                AnsiConsole.MarkupLine($"[green]Discovered mappings:[/] {discoveryResult.Mappings.Count}");
                AnsiConsole.MarkupLine($"[green]Compatible mappings written:[/] {compatibleMappings.Count}");
                if (droppedMappings.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]Dropped mappings:[/] {droppedMappings.Count}");
                    foreach (var dropped in droppedMappings.Take(25))
                    {
                        AnsiConsole.MarkupLine(
                            $"[yellow]  - {dropped.Mapping.Key.EscapeMarkup()} => {dropped.Status.EscapeMarkup()} ({(dropped.DiagnosticCode ?? "n/a").EscapeMarkup()})[/]");
                    }
                }

                if (options.Strict && droppedMappings.Count > 0)
                {
                    Utils.WriteError("--strict is enabled and one or more discovered mappings were dropped.");
                    return 1;
                }

                return 0;
            }
            finally
            {
                TryDeleteDirectory(temporaryDirectory);
            }
        }

        private static List<string> Validate(DuckTypeAotDiscoverMappingsOptions options)
        {
            var errors = new List<string>();
            if (options.ProxyAssemblies.Count == 0)
            {
                errors.Add("At least one --proxy-assembly must be provided.");
            }

            foreach (var proxyAssembly in options.ProxyAssemblies)
            {
                if (!File.Exists(proxyAssembly))
                {
                    errors.Add($"--proxy-assembly file was not found: {proxyAssembly}");
                }
            }

            foreach (var targetFolder in options.TargetFolders)
            {
                if (!Directory.Exists(targetFolder))
                {
                    errors.Add($"--target-folder directory was not found: {targetFolder}");
                }
            }

            if (options.TargetFolders.Count == 0)
            {
                errors.Add("At least one --target-folder must be provided.");
            }

            if (options.TargetFilters.Count == 0)
            {
                errors.Add("At least one --target-filter must be provided.");
            }

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                errors.Add("--output cannot be empty.");
            }

            return errors;
        }

        private static void WriteCanonicalMap(IEnumerable<DuckTypeAotMapping> mappings, string outputPath)
        {
            var canonical = new CanonicalMapDocument
            {
                SchemaVersion = "1",
                Mappings = mappings
                          .OrderBy(mapping => mapping.Key, StringComparer.Ordinal)
                          .Select(mapping => new CanonicalMapEntry
                          {
                              Mode = mapping.Mode == DuckTypeAotMappingMode.Reverse ? "reverse" : "forward",
                              ProxyType = mapping.ProxyTypeName,
                              ProxyAssembly = mapping.ProxyAssemblyName,
                              TargetType = mapping.TargetTypeName,
                              TargetAssembly = mapping.TargetAssemblyName
                          })
                          .ToList()
            };

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var json = JsonSerializer.Serialize(
                canonical,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
            File.WriteAllText(outputPath, json);
        }

        private static void WriteDiagnosticsReport(
            string? warningsReportPath,
            int discoveredMappingsCount,
            IReadOnlyList<DuckTypeAotMapping> compatibleMappings,
            IReadOnlyList<DiscoverDroppedMapping> droppedMappings,
            IReadOnlyList<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(warningsReportPath))
            {
                return;
            }

            var report = new DiscoverWarningsReport
            {
                DiscoveredMappings = discoveredMappingsCount,
                CompatibleMappings = compatibleMappings.Count,
                DroppedMappings = droppedMappings.ToList(),
                Warnings = warnings.ToList()
            };

            var directory = Path.GetDirectoryName(warningsReportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
            File.WriteAllText(warningsReportPath!, json);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temporary discovery output.
            }
        }

        private sealed class CanonicalMapDocument
        {
            public string SchemaVersion { get; set; } = "1";

            public List<CanonicalMapEntry> Mappings { get; set; } = new();
        }

        private sealed class CanonicalMapEntry
        {
            public string Mode { get; set; } = "forward";

            public string ProxyType { get; set; } = string.Empty;

            public string ProxyAssembly { get; set; } = string.Empty;

            public string TargetType { get; set; } = string.Empty;

            public string TargetAssembly { get; set; } = string.Empty;
        }

        private sealed class DiscoverWarningsReport
        {
            public int DiscoveredMappings { get; set; }

            public int CompatibleMappings { get; set; }

            public List<DiscoverDroppedMapping> DroppedMappings { get; set; } = new();

            public List<string> Warnings { get; set; } = new();
        }

        private sealed class DiscoverDroppedMapping
        {
            public DiscoverDroppedMapping(
                DuckTypeAotMapping mapping,
                string status,
                string? diagnosticCode,
                string? details)
            {
                Mapping = mapping;
                Status = status;
                DiagnosticCode = diagnosticCode;
                Details = details;
            }

            public DuckTypeAotMapping Mapping { get; }

            public string Status { get; }

            public string? DiagnosticCode { get; }

            public string? Details { get; }
        }
    }
}
