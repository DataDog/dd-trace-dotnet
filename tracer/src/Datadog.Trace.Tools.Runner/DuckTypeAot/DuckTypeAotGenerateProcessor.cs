// <copyright file="DuckTypeAotGenerateProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal static class DuckTypeAotGenerateProcessor
    {
        internal static int Process(DuckTypeAotGenerateOptions options)
        {
            var validationErrors = Validate(options);
            if (validationErrors.Count > 0)
            {
                foreach (var error in validationErrors)
                {
                    Utils.WriteError(error);
                }

                return 1;
            }

            var mappingResolutionResult = DuckTypeAotMappingResolver.Resolve(options);
            foreach (var warning in mappingResolutionResult.Warnings)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning}");
            }

            if (mappingResolutionResult.Errors.Count > 0)
            {
                foreach (var error in mappingResolutionResult.Errors)
                {
                    Utils.WriteError(error);
                }

                return 1;
            }

            if (mappingResolutionResult.Mappings.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No mappings were resolved from attributes/map file.");
            }

            var artifactPaths = DuckTypeAotArtifactPaths.Create(options);
            EnsureParentDirectoryExists(artifactPaths.OutputAssemblyPath);
            EnsureParentDirectoryExists(artifactPaths.ManifestPath);
            EnsureParentDirectoryExists(artifactPaths.CompatibilityMatrixPath);
            EnsureParentDirectoryExists(artifactPaths.CompatibilityReportPath);
            EnsureParentDirectoryExists(artifactPaths.TrimmerDescriptorPath);
            EnsureParentDirectoryExists(artifactPaths.PropsPath);

            try
            {
                var emissionResult = DuckTypeAotRegistryAssemblyEmitter.Emit(options, artifactPaths, mappingResolutionResult);
                var compatibilityArtifacts = DuckTypeAotArtifactsWriter.WriteAll(artifactPaths, mappingResolutionResult, emissionResult);

                AnsiConsole.MarkupLine($"[green]Generated registry assembly:[/] {artifactPaths.OutputAssemblyPath}");
                AnsiConsole.MarkupLine($"[green]Generated manifest:[/] {artifactPaths.ManifestPath}");
                AnsiConsole.MarkupLine($"[green]Generated trimmer descriptor:[/] {artifactPaths.TrimmerDescriptorPath}");
                AnsiConsole.MarkupLine($"[green]Generated props file:[/] {artifactPaths.PropsPath}");
                AnsiConsole.MarkupLine($"[green]Generated compatibility matrix:[/] {compatibilityArtifacts.MatrixPath}");
                AnsiConsole.MarkupLine($"[green]Generated compatibility report:[/] {compatibilityArtifacts.ReportPath}");

                if (compatibilityArtifacts.NonCompatibleMappings > 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]Compatibility status:[/] {compatibilityArtifacts.NonCompatibleMappings}/{compatibilityArtifacts.TotalMappings} mappings are not yet compatible.");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Utils.WriteError($"ducktype-aot generate failed: {ex.Message}");
                return 1;
            }
        }

        private static List<string> Validate(DuckTypeAotGenerateOptions options)
        {
            var errors = new List<string>();

            if (options.ProxyAssemblies.Count == 0)
            {
                errors.Add("At least one --proxy-assembly must be provided.");
            }

            if (options.TargetAssemblies.Count == 0 && options.TargetFolders.Count == 0)
            {
                errors.Add("At least one --target-assembly or --target-folder must be provided.");
            }

            if (options.TargetFilters.Count == 0)
            {
                errors.Add("At least one --target-filter must be provided.");
            }

            ValidateFileInputs(options.ProxyAssemblies, "--proxy-assembly", errors);
            ValidateFileInputs(options.TargetAssemblies, "--target-assembly", errors);
            ValidateDirectoryInputs(options.TargetFolders, "--target-folder", errors);
            ValidateOptionalFile(options.MapFile, "--map-file", errors);
            ValidateOptionalFile(options.MappingCatalog, "--mapping-catalog", errors);
            ValidateOptionalFile(options.GenericInstantiationsFile, "--generic-instantiations", errors);

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                errors.Add("--output cannot be empty.");
            }

            return errors;
        }

        private static void ValidateFileInputs(IReadOnlyList<string> paths, string optionName, List<string> errors)
        {
            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    errors.Add($"{optionName} file was not found: {path}");
                }
            }
        }

        private static void ValidateDirectoryInputs(IReadOnlyList<string> paths, string optionName, List<string> errors)
        {
            foreach (var path in paths)
            {
                if (!Directory.Exists(path))
                {
                    errors.Add($"{optionName} directory was not found: {path}");
                }
            }
        }

        private static void ValidateOptionalFile(string? path, string optionName, List<string> errors)
        {
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            {
                errors.Add($"{optionName} file was not found: {path}");
            }
        }

        private static void EnsureParentDirectoryExists(string path)
        {
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }
    }
}
