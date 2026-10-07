// <copyright file="DuckTypeAotGenerateProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Provides helper operations for duck type aot generate processor.
    /// </summary>
    internal static class DuckTypeAotGenerateProcessor
    {
        private const string ProfilingEnvironmentVariable = "DD_TRACE_DUCKTYPE_AOT_PROFILE";

        /// <summary>
        /// Executes process.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The computed numeric value.</returns>
        internal static int Process(DuckTypeAotGenerateOptions options)
        {
            var profilingEnabled = IsProfilingEnabled();
            var totalStopwatch = profilingEnabled ? Stopwatch.StartNew() : null;
            var validationErrors = Validate(options);
            if (validationErrors.Count > 0)
            {
                foreach (var error in validationErrors)
                {
                    Utils.WriteError(error);
                }

                return 1;
            }

            if (options.DiscoverMappings)
            {
                AnsiConsole.MarkupLine("[green]Discover step:[/] discovering and filtering compatible mappings before generation.");

                // Discovery only finds the mappings declared with attributes: an existing --map-file (e.g. the map recorded at
                // runtime) is kept, and the discovered mappings are added to it, under the lock a recording application takes
                // (a missing map is created like an empty one).
                var mapFileExists = File.Exists(options.MapFile);
                var discoveryOutputPath = $"{options.MapFile}.{Guid.NewGuid():N}.discovered.json";
                try
                {
                    int discoveryExitCode;
                    try
                    {
                        discoveryExitCode = DuckTypeAotDiscoverMappingsProcessor.Process(
                            new DuckTypeAotDiscoverMappingsOptions(
                                options.ProxyAssemblies,
                                options.TargetFolders,
                                options.TargetFilters,
                                discoveryOutputPath,
                                warningsReportPath: null,
                                strict: false));
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning:[/] discovery failed: {ex.Message.EscapeMarkup()}");
                        discoveryExitCode = 1;
                    }

                    if (discoveryExitCode != 0)
                    {
                        if (!mapFileExists)
                        {
                            Utils.WriteError("ducktype-aot generate failed during discovery pre-step.");
                            return 1;
                        }

                        // The existing map is still a valid input: generate it without the mappings declared with attributes.
                        AnsiConsole.MarkupLine($"[yellow]Warning:[/] discovery failed, so no mapping declared with attributes was added to {options.MapFile!.EscapeMarkup()}.");
                    }
                    else
                    {
                        if (!TryMergeDiscoveredMappings(options.MapFile!, discoveryOutputPath!, out var addedMappings, out var mergeError))
                        {
                            Utils.WriteError($"ducktype-aot generate failed to add the discovered mappings to --map-file: {mergeError}");
                            return 1;
                        }

                        AnsiConsole.MarkupLine($"[green]Discover step:[/] added {addedMappings} discovered mapping(s) to {options.MapFile!.EscapeMarkup()}.");
                    }
                }
                finally
                {
                    try
                    {
                        File.Delete(discoveryOutputPath);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning:[/] the discovery output {discoveryOutputPath.EscapeMarkup()} couldn't be deleted: {ex.Message.EscapeMarkup()}");
                    }
                }
            }

            var resolveStopwatch = profilingEnabled ? Stopwatch.StartNew() : null;
            var mappingResolutionResult = DuckTypeAotMappingResolver.Resolve(options);
            if (resolveStopwatch is not null)
            {
                resolveStopwatch.Stop();
                WriteProfileMetric($"resolve={resolveStopwatch.Elapsed.TotalSeconds:F3}s mappings={mappingResolutionResult.Mappings.Count} proxyAssemblies={mappingResolutionResult.ProxyAssemblyPathsByName.Count} targetAssemblies={mappingResolutionResult.TargetAssemblyPathsByName.Count}");
            }

            foreach (var warning in mappingResolutionResult.Warnings)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning.EscapeMarkup()}");
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
                Utils.WriteError("No mappings were resolved from --map-file.");
                return 1;
            }

            var signingKeyFilePath = ResolveStrongNameKeyFilePath(options);
            if (StringUtil.IsNullOrWhiteSpace(signingKeyFilePath))
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No strong-name key configured. The generated registry assembly will be unsigned.");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Strong-name signing key:[/] {signingKeyFilePath.EscapeMarkup()}");
                options = new DuckTypeAotGenerateOptions(
                    options.ProxyAssemblies,
                    options.TargetAssemblies,
                    options.TargetFolders,
                    options.TargetFilters,
                    mapFile: options.MapFile,
                    genericInstantiationsFile: options.GenericInstantiationsFile,
                    outputPath: options.OutputPath,
                    assemblyName: options.AssemblyName,
                    trimmerDescriptorPath: options.TrimmerDescriptorPath,
                    propsPath: options.PropsPath,
                    strongNameKeyFile: signingKeyFilePath,
                    discoverMappings: options.DiscoverMappings);
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
                var emitStopwatch = profilingEnabled ? Stopwatch.StartNew() : null;
                var emissionResult = DuckTypeAotRegistryAssemblyEmitter.Emit(options, artifactPaths, mappingResolutionResult);
                if (emitStopwatch is not null)
                {
                    emitStopwatch.Stop();
                    WriteProfileMetric($"emit={emitStopwatch.Elapsed.TotalSeconds:F3}s canonicalMappings={mappingResolutionResult.Mappings.Count} runtimeRegistrations={emissionResult.RuntimeRegistrations.Count}");
                }

                foreach (var warning in emissionResult.Warnings)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning.EscapeMarkup()}");
                }

                var artifactsStopwatch = profilingEnabled ? Stopwatch.StartNew() : null;
                var compatibilityArtifacts = DuckTypeAotArtifactsWriter.WriteAll(artifactPaths, mappingResolutionResult, emissionResult);
                if (artifactsStopwatch is not null)
                {
                    artifactsStopwatch.Stop();
                    WriteProfileMetric($"artifacts={artifactsStopwatch.Elapsed.TotalSeconds:F3}s totalMappings={compatibilityArtifacts.TotalMappings} nonCompatible={compatibilityArtifacts.NonCompatibleMappings}");
                }

                AnsiConsole.MarkupLine($"[green]Generated registry assembly:[/] {artifactPaths.OutputAssemblyPath.EscapeMarkup()}");
                AnsiConsole.MarkupLine($"[green]Generated manifest:[/] {artifactPaths.ManifestPath.EscapeMarkup()}");
                AnsiConsole.MarkupLine($"[green]Generated trimmer descriptor:[/] {artifactPaths.TrimmerDescriptorPath.EscapeMarkup()}");
                AnsiConsole.MarkupLine($"[green]Generated props file:[/] {artifactPaths.PropsPath.EscapeMarkup()}");
                AnsiConsole.MarkupLine($"[green]Generated compatibility matrix:[/] {compatibilityArtifacts.MatrixPath.EscapeMarkup()}");
                AnsiConsole.MarkupLine($"[green]Generated compatibility report:[/] {compatibilityArtifacts.ReportPath.EscapeMarkup()}");

                if (compatibilityArtifacts.ReplayedDynamicFailureMappings > 0)
                {
                    AnsiConsole.MarkupLine($"[green]Compatibility status:[/] {compatibilityArtifacts.ReplayedDynamicFailureMappings}/{compatibilityArtifacts.TotalMappings} mappings fail in dynamic duck typing too, and the registry replays that failure.");
                }

                if (compatibilityArtifacts.NonCompatibleMappings > 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]Compatibility status:[/] {compatibilityArtifacts.NonCompatibleMappings}/{compatibilityArtifacts.TotalMappings} mappings are not yet compatible.");
                }

                if (totalStopwatch is not null)
                {
                    totalStopwatch.Stop();
                    WriteProfileMetric($"total={totalStopwatch.Elapsed.TotalSeconds:F3}s");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Utils.WriteError($"ducktype-aot generate failed: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Adds the mappings of a discovered map to an existing map file. The file is edited as text, so everything else in it
        /// (its entries, comments, formatting, encoding) is kept.
        /// </summary>
        /// <param name="mapFilePath">The existing map file.</param>
        /// <param name="discoveredMapPath">The discovered map file.</param>
        /// <param name="addedMappings">The number of mappings added.</param>
        /// <param name="error">Why the maps couldn't be merged.</param>
        /// <returns>true if the map file was updated; otherwise, false.</returns>
        internal static bool TryMergeDiscoveredMappings(string mapFilePath, string discoveredMapPath, out int addedMappings, out string? error)
        {
            addedMappings = 0;
            error = null;

            // An application recording mappings at runtime may update the same map: take the lock the recorder takes, and
            // replace the map atomically like it does.
            using var mapLock = DuckTypeAotDiscoveryRecorder.AcquireOutputLock(mapFilePath, TimeSpan.FromSeconds(30));
            if (mapLock is null)
            {
                error = $"'{mapFilePath}' could not be locked ('{DuckTypeAotDiscoveryRecorder.GetOutputLockPath(mapFilePath)}').";
                return false;
            }

            var discoveredMap = DuckTypeAotMapFileParser.Parse(discoveredMapPath);
            if (discoveredMap.Errors.Count > 0)
            {
                error = string.Join(" ", discoveredMap.Errors);
                return false;
            }

            // Decoded like the map parser reads it, and written back with the same encoding. A missing map is created.
            var text = string.Empty;
            Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            try
            {
                if (File.Exists(mapFilePath) || Directory.Exists(mapFilePath))
                {
                    var bytes = File.ReadAllBytes(mapFilePath);
                    using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
                    text = reader.ReadToEnd();
                    encoding = reader.CurrentEncoding;
                }
            }
            catch (DecoderFallbackException)
            {
                // Rewriting it would replace what isn't UTF-8 (e.g. UTF-16 without a byte order mark).
                error = $"'{mapFilePath}' can't be read: it isn't UTF-8, and has no byte order mark telling its encoding.";
                return false;
            }
            catch (Exception ex)
            {
                error = $"'{mapFilePath}' can't be read: {ex.Message}";
                return false;
            }

            if (text.IndexOf('\0') >= 0)
            {
                // ASCII text in UTF-16 or UTF-32 without a byte order mark is valid UTF-8 with NUL characters, which JSON can't
                // have: rewriting it would lose its mappings.
                error = $"'{mapFilePath}' can't be read: it isn't UTF-8 (it has NUL characters), and has no byte order mark telling its encoding.";
                return false;
            }

            var newLine = text.IndexOf('\n') >= 0 ? (text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n") : text.IndexOf('\r') >= 0 ? "\r" : "\n";
            var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
            JObject? document = null;
            JProperty? mappingsProperty = null;
            if (!StringUtil.IsNullOrWhiteSpace(text))
            {
                // The map has to be one the map parser reads (a map without mappings yet gets a 'mappings' array).
                var existingMap = DuckTypeAotMapFileParser.ParseText(text, mapFilePath, requireMappings: false);
                try
                {
                    document = LoadMapDocument(text);
                }
                catch (Exception ex) when (existingMap.Errors.Count == 0)
                {
                    error = $"'{mapFilePath}' can't be read: {ex.Message}";
                    return false;
                }
                catch (Exception)
                {
                    document = null;
                }

                // The mappings are added to its 'mappings' array: one, which the parser reads whatever its casing.
                var mappingsProperties = document?.Properties().Where(property => string.Equals(property.Name, "mappings", StringComparison.OrdinalIgnoreCase)).ToList();
                if (mappingsProperties is not null &&
                    (CountRootMappingsProperties(text) > 1 || mappingsProperties.Any(property => property.Value is not JArray)))
                {
                    error = $"'{mapFilePath}' must have a single 'mappings' array.";
                    return false;
                }

                if (existingMap.Errors.Count > 0)
                {
                    error = string.Join(" ", existingMap.Errors);
                    return false;
                }

                expectedKeys.UnionWith(existingMap.Mappings.Select(mapping => mapping.Key));
                mappingsProperty = mappingsProperties?.FirstOrDefault();
            }

            var addedMappingsToWrite = discoveredMap.Mappings.Where(mapping => expectedKeys.Add(mapping.Key)).ToList();
            addedMappings = addedMappingsToWrite.Count;
            if (addedMappings == 0)
            {
                return true;
            }

            var addedEntries = addedMappingsToWrite.Select(
                                                       mapping => JsonConvert.SerializeObject(new
                                                       {
                                                           mode = mapping.Mode == DuckTypeAotMappingMode.Reverse ? "reverse" : "forward",
                                                           proxyType = mapping.ProxyTypeName,
                                                           proxyAssembly = mapping.ProxyAssemblyName,
                                                           targetType = mapping.TargetTypeName,
                                                           targetAssembly = mapping.TargetAssemblyName
                                                       }))
                                                   .ToList();
            string? newText;
            if (document is null)
            {
                // An empty map (e.g. a file just created) is written as a new map.
                newText = "{" + newLine + "  \"mappings\": [" + newLine + "    " + string.Join("," + newLine + "    ", addedEntries) + newLine + "  ]" + newLine + "}" + newLine;
            }
            else if (!MapFileInsertionPoint.TryFind(text, out var insertionPoint) || !IsMerged(newText = insertionPoint.Insert(text, addedEntries, newLine)))
            {
                // JSON the text edit doesn't handle (e.g. a constructor, like new Date(...), which Json.NET reads): the map is
                // written back from its document, which loses its comments, and only when that keeps its numbers as written.
                if (mappingsProperty is null)
                {
                    mappingsProperty = new JProperty("mappings", new JArray());
                    document.Add(mappingsProperty);
                }

                foreach (var addedEntry in addedEntries)
                {
                    ((JArray)mappingsProperty.Value).Add(JObject.Parse(addedEntry));
                }

                newText = document.ToString(Formatting.Indented);
                if (!IsMerged(newText) || !ReadNumberLiterals(text).SequenceEqual(ReadNumberLiterals(newText), StringComparer.Ordinal))
                {
                    error = $"The discovered mappings couldn't be added to '{mapFilePath}': add them to its 'mappings' array.";
                    return false;
                }
            }

#if NET7_0_OR_GREATER
            // The map is replaced by a new file: it keeps the permissions of the one it replaces (e.g. merged by root in a container).
            UnixFileMode? fileMode = null;
            if (!OperatingSystem.IsWindows() && File.Exists(mapFilePath))
            {
                try
                {
                    fileMode = File.GetUnixFileMode(DuckTypeAotDiscoveryRecorder.ResolveMapPath(mapFilePath));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
#endif

            try
            {
                DuckTypeAotDiscoveryRecorder.WriteAtomically(mapFilePath, newText, encoding);
            }
            catch (Exception ex)
            {
                error = $"'{mapFilePath}' can't be written: {ex.Message}";
                return false;
            }

#if NET7_0_OR_GREATER
            if (fileMode is { } mode && !OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(DuckTypeAotDiscoveryRecorder.ResolveMapPath(mapFilePath), mode);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
#endif

            return true;

            bool IsMerged(string candidate)
            {
                // The map has to read back as its mappings and the discovered ones.
                var merged = DuckTypeAotMapFileParser.ParseText(candidate, mapFilePath);
                return merged.Errors.Count == 0 && expectedKeys.SetEquals(merged.Mappings.Select(mapping => mapping.Key));
            }

            static JObject LoadMapDocument(string text)
            {
                // Dates and decimal numbers are kept as written if the map is written back from its document; numbers beyond the
                // range of decimal (which the map parser reads) are read as doubles.
                try
                {
                    return LoadMapDocumentWith(text, FloatParseHandling.Decimal);
                }
                catch (JsonReaderException)
                {
                    return LoadMapDocumentWith(text, FloatParseHandling.Double);
                }
            }

            static JObject LoadMapDocumentWith(string text, FloatParseHandling floatParseHandling)
                => JObject.Load(
                    new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = floatParseHandling },
                    new JsonLoadSettings { CommentHandling = CommentHandling.Ignore, DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace });
        }

        /// <summary>
        /// Reads the number literals of a JSON text, as Json.NET reads it (with comments, strings quoted with double or single
        /// quotes, unquoted names, NaN and Infinity), in order.
        /// </summary>
        /// <param name="text">The JSON text.</param>
        /// <returns>The number literals, as written.</returns>
        internal static List<string> ReadNumberLiterals(string text)
        {
            var literals = new List<string>();
            var position = 0;
            while (position < text.Length)
            {
                var character = text[position];
                if (character is '"' or '\'')
                {
                    position++;
                    while (position < text.Length && text[position] != character)
                    {
                        position += text[position] == '\\' ? 2 : 1;
                    }

                    position++;
                }
                else if (character == '/' && position + 1 < text.Length && text[position + 1] == '/')
                {
                    while (position < text.Length && text[position] != '\n')
                    {
                        position++;
                    }
                }
                else if (character == '/' && position + 1 < text.Length && text[position + 1] == '*')
                {
                    var end = text.IndexOf("*/", position + 2, StringComparison.Ordinal);
                    position = end < 0 ? text.Length : end + 2;
                }
                else if (IsWordCharacter(character))
                {
                    var start = position;
                    while (position < text.Length && IsWordCharacter(text[position]))
                    {
                        position++;
                    }

                    var word = text.Substring(start, position - start);
                    if (char.IsDigit(word[0]) || word[0] is '-' or '+' or '.' || word is "NaN" or "Infinity")
                    {
                        literals.Add(word);
                    }
                }
                else
                {
                    position++;
                }
            }

            return literals;

            static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character is '-' or '+' or '.' or '_' or '$';
        }

        internal static bool IsProfilingEnabled()
        {
            var value = Environment.GetEnvironmentVariable(ProfilingEnvironmentVariable);
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        internal static void WriteProfileMetric(string message)
        {
            AnsiConsole.MarkupLine($"[blue]ducktype-aot profile:[/] {message.EscapeMarkup()}");
        }

        /// <summary>
        /// Validates validate.
        /// </summary>
        /// <param name="options">The options value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static List<string> Validate(DuckTypeAotGenerateOptions options)
        {
            var errors = new List<string>();

            if (options.ProxyAssemblies.Count == 0)
            {
                errors.Add("At least one --proxy-assembly must be provided.");
            }

            // Keep processor compatibility for direct API callers that still pass explicit target assemblies.
            // The CLI now only surfaces --target-folder, but tests and programmatic usage may still provide TargetAssemblies.
            if (options.TargetFolders.Count == 0 && options.TargetAssemblies.Count == 0)
            {
                errors.Add("At least one target resolution source must be provided (target folder).");
            }

            if (options.TargetFilters.Count == 0)
            {
                errors.Add("At least one --target-filter must be provided.");
            }

            ValidateFileInputs(options.ProxyAssemblies, "--proxy-assembly", errors);
            ValidateFileInputs(options.TargetAssemblies, "target assembly", errors);
            ValidateDirectoryInputs(options.TargetFolders, "--target-folder", errors);
            if (StringUtil.IsNullOrWhiteSpace(options.MapFile))
            {
                errors.Add("--map-file is required.");
            }
            else if (!options.DiscoverMappings)
            {
                ValidateOptionalFile(options.MapFile, "--map-file", errors);
            }

            ValidateOptionalFile(options.GenericInstantiationsFile, "--generic-instantiations", errors);
            ValidateOptionalFile(options.StrongNameKeyFile, "--strong-name-key-file", errors);

            if (StringUtil.IsNullOrWhiteSpace(options.OutputPath))
            {
                errors.Add("--output cannot be empty.");
            }

            var environmentStrongNameKeyFile = Environment.GetEnvironmentVariable("DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE");
            if (StringUtil.IsNullOrWhiteSpace(options.StrongNameKeyFile) &&
                !StringUtil.IsNullOrWhiteSpace(environmentStrongNameKeyFile) &&
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
        private static string? ResolveStrongNameKeyFilePath(DuckTypeAotGenerateOptions options)
        {
            if (!StringUtil.IsNullOrWhiteSpace(options.StrongNameKeyFile))
            {
                return Path.GetFullPath(options.StrongNameKeyFile!);
            }

            var environmentPath = Environment.GetEnvironmentVariable("DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE");
            if (StringUtil.IsNullOrWhiteSpace(environmentPath))
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
        private static void ValidateOptionalFile(string? path, string optionName, List<string> errors)
        {
            if (!StringUtil.IsNullOrWhiteSpace(path) && !File.Exists(path))
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
            if (!StringUtil.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }

        /// <summary>
        /// Counts the 'mappings' properties of the root object of a map, whatever their casing (duplicates included).
        /// </summary>
        /// <param name="text">The content of the map.</param>
        /// <returns>The number of 'mappings' properties.</returns>
        private static int CountRootMappingsProperties(string text)
        {
            var count = 0;
            using var reader = new JsonTextReader(new StringReader(text));
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName && reader.Depth == 1 && string.Equals(reader.Value as string, "mappings", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
                else if (reader.TokenType == JsonToken.EndObject && reader.Depth == 0)
                {
                    // The root object ends: what follows is the parser's to reject.
                    break;
                }
            }

            return count;
        }

        /// <summary>
        /// Where mappings are added to the text of a map file: after the last element of its 'mappings' array (in any casing,
        /// like the map parser reads it), or in a new 'mappings' array after the last property of the root object.
        /// </summary>
        private sealed class MapFileInsertionPoint
        {
            private readonly int _index;
            private readonly bool _afterMember;
            private readonly string _indentation;

            private MapFileInsertionPoint(int index, bool inMappingsArray, bool afterMember, string indentation)
            {
                _index = index;
                InMappingsArray = inMappingsArray;
                _afterMember = afterMember;
                _indentation = indentation;
            }

            /// <summary>
            /// Gets a value indicating whether the map has a 'mappings' array the entries are added to.
            /// </summary>
            public bool InMappingsArray { get; }

            /// <summary>
            /// Finds where the mappings are added to the text of a map file (a JSON object, with comments).
            /// </summary>
            /// <param name="text">The text of the map file.</param>
            /// <param name="insertionPoint">The insertion point.</param>
            /// <returns>true if the text is a JSON object; otherwise, false.</returns>
            public static bool TryFind(string text, out MapFileInsertionPoint insertionPoint)
            {
                insertionPoint = null!;
                var position = 0;
                SkipTrivia(text, ref position);
                if (position >= text.Length || text[position] != '{')
                {
                    return false;
                }

                var rootStart = position++;
                var lastMemberEnd = -1;
                while (true)
                {
                    SkipTrivia(text, ref position);
                    if (position >= text.Length)
                    {
                        return false;
                    }

                    switch (text[position])
                    {
                        case '}':
                            // No 'mappings' array: one is added after the last property.
                            var propertyAnchor = lastMemberEnd >= 0 ? lastMemberEnd : rootStart;
                            insertionPoint = new MapFileInsertionPoint(lastMemberEnd >= 0 ? lastMemberEnd : rootStart + 1, inMappingsArray: false, lastMemberEnd >= 0, lastMemberEnd >= 0 ? GetIndentation(text, propertyAnchor) : GetIndentation(text, rootStart) + "  ");
                            return true;
                        case ',':
                            position++;
                            continue;
                        default:
                            // Like the map parser (Json.NET), names can be quoted with double or single quotes, or not quoted.
                            if (!TryReadName(text, ref position, out var name))
                            {
                                return false;
                            }

                            SkipTrivia(text, ref position);
                            if (position >= text.Length || text[position] != ':')
                            {
                                return false;
                            }

                            position++;
                            SkipTrivia(text, ref position);
                            if (string.Equals(name, "mappings", StringComparison.OrdinalIgnoreCase) && position < text.Length && text[position] == '[')
                            {
                                var arrayStart = position++;
                                var lastElementEnd = -1;
                                while (true)
                                {
                                    SkipTrivia(text, ref position);
                                    if (position >= text.Length)
                                    {
                                        return false;
                                    }

                                    if (text[position] == ']')
                                    {
                                        insertionPoint = lastElementEnd >= 0
                                                             ? new MapFileInsertionPoint(lastElementEnd, inMappingsArray: true, afterMember: true, GetIndentation(text, lastElementEnd))
                                                             : new MapFileInsertionPoint(arrayStart + 1, inMappingsArray: true, afterMember: false, GetIndentation(text, arrayStart) + "  ");
                                        return true;
                                    }

                                    if (text[position] == ',')
                                    {
                                        position++;
                                        continue;
                                    }

                                    if (!SkipValue(text, ref position))
                                    {
                                        return false;
                                    }

                                    lastElementEnd = position;
                                }
                            }

                            if (!SkipValue(text, ref position))
                            {
                                return false;
                            }

                            lastMemberEnd = position;
                            continue;
                    }
                }
            }

            /// <summary>
            /// Inserts mapping entries in the text of the map file.
            /// </summary>
            /// <param name="text">The text of the map file.</param>
            /// <param name="entries">The JSON of the mappings to add.</param>
            /// <param name="newLine">The new line of the map file.</param>
            /// <returns>The text with the mappings added.</returns>
            public string Insert(string text, IReadOnlyList<string> entries, string newLine)
            {
                string inserted;
                if (InMappingsArray)
                {
                    var elements = string.Join("," + newLine + _indentation, entries);
                    inserted = _afterMember
                                   ? "," + newLine + _indentation + elements
                                   : newLine + _indentation + elements + newLine + _indentation.Substring(0, Math.Max(0, _indentation.Length - 2));
                }
                else
                {
                    var elementIndentation = _indentation + "  ";
                    var property = "\"mappings\": [" + newLine + elementIndentation + string.Join("," + newLine + elementIndentation, entries) + newLine + _indentation + "]";
                    inserted = _afterMember
                                   ? "," + newLine + _indentation + property
                                   : newLine + _indentation + property + newLine;
                }

                return text.Substring(0, _index) + inserted + text.Substring(_index);
            }

            private static string GetIndentation(string text, int position)
            {
                var lineStart = position;
                while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
                {
                    lineStart--;
                }

                var indentationEnd = lineStart;
                while (indentationEnd < text.Length && (text[indentationEnd] == ' ' || text[indentationEnd] == '\t'))
                {
                    indentationEnd++;
                }

                return text.Substring(lineStart, indentationEnd - lineStart);
            }

            private static void SkipTrivia(string text, ref int position)
            {
                while (position < text.Length)
                {
                    if (char.IsWhiteSpace(text[position]))
                    {
                        position++;
                    }
                    else if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/')
                    {
                        while (position < text.Length && text[position] != '\n' && text[position] != '\r')
                        {
                            position++;
                        }
                    }
                    else if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '*')
                    {
                        var end = text.IndexOf("*/", position + 2, StringComparison.Ordinal);
                        position = end < 0 ? text.Length : end + 2;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            private static bool TryReadName(string text, ref int position, out string name)
            {
                if (text[position] is '"' or '\'')
                {
                    name = ReadString(text, ref position);
                    return true;
                }

                var start = position;
                while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] is '_' or '$'))
                {
                    position++;
                }

                name = text.Substring(start, position - start);
                return position > start;
            }

            private static string ReadString(string text, ref int position)
            {
                var value = new StringBuilder();
                var quote = text[position];
                position++;
                while (position < text.Length && text[position] != quote)
                {
                    if (text[position] == '\\' && position + 1 < text.Length)
                    {
                        position++;
                        if (text[position] == 'u' && position + 4 < text.Length &&
                            int.TryParse(text.Substring(position + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        {
                            value.Append((char)code);
                            position += 5;
                            continue;
                        }
                    }

                    value.Append(text[position++]);
                }

                position++;
                return value.ToString();
            }

            private static bool SkipValue(string text, ref int position)
            {
                if (position >= text.Length)
                {
                    return false;
                }

                switch (text[position])
                {
                    case '"':
                    case '\'':
                        ReadString(text, ref position);
                        return position <= text.Length;
                    case '{':
                    case '[':
                        var closing = text[position] == '{' ? '}' : ']';
                        position++;
                        while (true)
                        {
                            SkipTrivia(text, ref position);
                            if (position >= text.Length)
                            {
                                return false;
                            }

                            if (text[position] == closing)
                            {
                                position++;
                                return true;
                            }

                            if (text[position] == ',' || text[position] == ':')
                            {
                                position++;
                            }
                            else if (!SkipValue(text, ref position))
                            {
                                return false;
                            }
                        }

                    default:
                        var start = position;
                        while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not (',' or ']' or '}' or '/' or ':'))
                        {
                            position++;
                        }

                        return position > start;
                }
            }
        }
    }
}
