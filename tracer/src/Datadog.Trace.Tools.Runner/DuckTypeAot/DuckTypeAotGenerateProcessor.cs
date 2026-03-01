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
    /// <summary>
    /// Provides helper operations for duck type aot generate processor.
    /// </summary>
    internal static class DuckTypeAotGenerateProcessor
    {
        /// <summary>
        /// Executes process.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The computed numeric value.</returns>
        internal static int Process(DuckTypeAotGenerateOptions options)
        {
            var validationErrors = Validate(options);
            // Branch: take this path when (validationErrors.Count > 0) evaluates to true.
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

            // Branch: take this path when (mappingResolutionResult.Errors.Count > 0) evaluates to true.
            if (mappingResolutionResult.Errors.Count > 0)
            {
                foreach (var error in mappingResolutionResult.Errors)
                {
                    Utils.WriteError(error);
                }

                return 1;
            }

            // Branch: take this path when (mappingResolutionResult.Mappings.Count == 0) evaluates to true.
            if (mappingResolutionResult.Mappings.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No mappings were resolved from attributes/map file.");
            }

            var signingKeyFilePath = ResolveStrongNameKeyFilePath(options);
            // Branch: take this path when (string.IsNullOrWhiteSpace(signingKeyFilePath)) evaluates to true.
            if (string.IsNullOrWhiteSpace(signingKeyFilePath))
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No strong-name key configured. The generated registry assembly will be unsigned.");
            }
            else
            {
                // Branch: fallback path when earlier branch conditions evaluate to false.
                AnsiConsole.MarkupLine($"[green]Strong-name signing key:[/] {signingKeyFilePath}");
                options = new DuckTypeAotGenerateOptions(
                    options.ProxyAssemblies,
                    options.TargetAssemblies,
                    options.TargetFolders,
                    options.TargetFilters,
                    options.MapFile,
                    options.MappingCatalog,
                    options.GenericInstantiationsFile,
                    options.OutputPath,
                    options.AssemblyName,
                    options.TrimmerDescriptorPath,
                    options.PropsPath,
                    options.RequireMappingCatalog,
                    signingKeyFilePath);
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

                // Branch: take this path when (compatibilityArtifacts.NonCompatibleMappings > 0) evaluates to true.
                if (compatibilityArtifacts.NonCompatibleMappings > 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]Compatibility status:[/] {compatibilityArtifacts.NonCompatibleMappings}/{compatibilityArtifacts.TotalMappings} mappings are not yet compatible.");
                }

                return 0;
            }
            catch (Exception ex)
            {
                // Branch: handles exceptions that match Exception ex.
                Utils.WriteError($"ducktype-aot generate failed: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Validates validate.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static List<string> Validate(DuckTypeAotGenerateOptions options)
        {
            var errors = new List<string>();

            // Branch: take this path when (options.ProxyAssemblies.Count == 0) evaluates to true.
            if (options.ProxyAssemblies.Count == 0)
            {
                errors.Add("At least one --proxy-assembly must be provided.");
            }

            // Branch: take this path when (options.TargetAssemblies.Count == 0 && options.TargetFolders.Count == 0) evaluates to true.
            if (options.TargetAssemblies.Count == 0 && options.TargetFolders.Count == 0)
            {
                errors.Add("At least one --target-assembly or --target-folder must be provided.");
            }

            // Branch: take this path when (options.TargetFilters.Count == 0) evaluates to true.
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
            ValidateOptionalFile(options.StrongNameKeyFile, "--strong-name-key-file", errors);

            // Branch: take this path when (options.RequireMappingCatalog && string.IsNullOrWhiteSpace(options.MappingCatalog)) evaluates to true.
            if (options.RequireMappingCatalog && string.IsNullOrWhiteSpace(options.MappingCatalog))
            {
                errors.Add("--mapping-catalog is required when --require-mapping-catalog is enabled.");
            }

            // Branch: take this path when (string.IsNullOrWhiteSpace(options.OutputPath)) evaluates to true.
            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                errors.Add("--output cannot be empty.");
            }

            var environmentStrongNameKeyFile = Environment.GetEnvironmentVariable("DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE");
            // Branch: take this path when (string.IsNullOrWhiteSpace(options.StrongNameKeyFile) && evaluates to true.
            if (string.IsNullOrWhiteSpace(options.StrongNameKeyFile) &&
                !string.IsNullOrWhiteSpace(environmentStrongNameKeyFile) &&
                !File.Exists(environmentStrongNameKeyFile))
            {
                errors.Add($"Strong-name key file from DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE was not found: {environmentStrongNameKeyFile}");
            }

            return errors;
        }

        /// <summary>
        /// Resolves resolve strong name key file path.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The result produced by this operation.</returns>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static string? ResolveStrongNameKeyFilePath(DuckTypeAotGenerateOptions options)
        {
            // Branch: take this path when (!string.IsNullOrWhiteSpace(options.StrongNameKeyFile)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(options.StrongNameKeyFile))
            {
                return Path.GetFullPath(options.StrongNameKeyFile!);
            }

            var environmentPath = Environment.GetEnvironmentVariable("DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE");
            // Branch: take this path when (string.IsNullOrWhiteSpace(environmentPath)) evaluates to true.
            if (string.IsNullOrWhiteSpace(environmentPath))
            {
                return null;
            }

            return Path.GetFullPath(environmentPath);
        }

        /// <summary>
        /// Validates validate file inputs.
        /// </summary>
        /// <param name="paths">The paths value.</param>
        /// <param name="optionName">The option name value.</param>
        /// <param name="errors">The errors value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void ValidateFileInputs(IReadOnlyList<string> paths, string optionName, List<string> errors)
        {
            foreach (var path in paths)
            {
                // Branch: take this path when (!File.Exists(path)) evaluates to true.
                if (!File.Exists(path))
                {
                    errors.Add($"{optionName} file was not found: {path}");
                }
            }
        }

        /// <summary>
        /// Validates validate directory inputs.
        /// </summary>
        /// <param name="paths">The paths value.</param>
        /// <param name="optionName">The option name value.</param>
        /// <param name="errors">The errors value.</param>
        private static void ValidateDirectoryInputs(IReadOnlyList<string> paths, string optionName, List<string> errors)
        {
            foreach (var path in paths)
            {
                // Branch: take this path when (!Directory.Exists(path)) evaluates to true.
                if (!Directory.Exists(path))
                {
                    errors.Add($"{optionName} directory was not found: {path}");
                }
            }
        }

        /// <summary>
        /// Validates validate optional file.
        /// </summary>
        /// <param name="path">The path value.</param>
        /// <param name="optionName">The option name value.</param>
        /// <param name="errors">The errors value.</param>
        /// <remarks>Emits or composes IL for generated duck-typing proxy operations.</remarks>
        private static void ValidateOptionalFile(string? path, string optionName, List<string> errors)
        {
            // Branch: take this path when (!string.IsNullOrWhiteSpace(path) && !File.Exists(path)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            {
                errors.Add($"{optionName} file was not found: {path}");
            }
        }

        /// <summary>
        /// Ensures ensure parent directory exists.
        /// </summary>
        /// <param name="path">The path value.</param>
        private static void EnsureParentDirectoryExists(string path)
        {
            var parent = Path.GetDirectoryName(path);
            // Branch: take this path when (!string.IsNullOrWhiteSpace(parent)) evaluates to true.
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }
    }
}
