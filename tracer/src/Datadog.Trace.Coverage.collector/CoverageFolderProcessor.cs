// <copyright file="CoverageFolderProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage.Exceptions;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Formatting = Datadog.Trace.Vendors.Newtonsoft.Json.Formatting;

namespace Datadog.Trace.Coverage.Collector
{
    /// <summary>
    /// Rewrites the assemblies of a folder for code coverage and registers Datadog.Trace in the dependency manifests.
    /// Shared by the VSTest data collector and by the runner, which does it before launching a Microsoft.Testing.Platform run.
    /// </summary>
    internal static class CoverageFolderProcessor
    {
        /// <summary>
        /// Rewrites the assemblies of a folder that have symbols.
        /// </summary>
        /// <param name="folder">Folder with the assemblies.</param>
        /// <param name="searchOption">Whether to include subfolders.</param>
        /// <param name="settings">Coverage settings.</param>
        /// <param name="logger">Logger used by the rewrite, or null to use the console.</param>
        /// <returns>The number of assemblies that were processed.</returns>
        public static int Process(string folder, SearchOption searchOption, CoverageSettings settings, ICollectorLogger? logger)
        {
            var processedDirectories = new HashSet<string>();
            var numAssemblies = 0;
            var tracerAssemblyName = typeof(Tracer).Assembly.GetName().Name;

            // Process assemblies in parallel.
            Parallel.ForEach(
                Directory.EnumerateFiles(folder, "*.*", searchOption),
                file =>
                {
                    var path = Path.GetDirectoryName(file)!;
                    var fileWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    // Skip the Datadog.Trace assembly
                    if (tracerAssemblyName == fileWithoutExtension)
                    {
                        return;
                    }

                    if (HasAssemblyExtension(file))
                    {
                        if (File.Exists(Path.Combine(path, fileWithoutExtension + ".pdb")) || File.Exists(Path.Combine(path, fileWithoutExtension + ".PDB")))
                        {
                            const int maxAttempts = 3;
                            List<Exception>? exceptions = null;
                            for (var attempt = 1; attempt <= maxAttempts; attempt++)
                            {
                                try
                                {
                                    var asmProcessor = new AssemblyProcessor(file, settings, logger);
                                    asmProcessor.Process();
                                    Interlocked.Increment(ref numAssemblies);
                                    if (asmProcessor.HasTracerAssemblyCopied)
                                    {
                                        lock (processedDirectories)
                                        {
                                            processedDirectories.Add(Path.GetDirectoryName(file) ?? string.Empty);
                                        }
                                    }

                                    exceptions = null;
                                    break;
                                }
                                catch (PdbNotFoundException)
                                {
                                    // If the PDB file was not found, we skip the assembly without throwing error.
                                    logger?.Debug($"{nameof(PdbNotFoundException)} processing file: {file}");
                                    exceptions = null;
                                    break;
                                }
                                catch (BadImageFormatException)
                                {
                                    // If the Assembly has not the correct format (eg. native dll / exe)
                                    // We skip processing the assembly.
                                    logger?.Debug($"{nameof(BadImageFormatException)} processing file: {file}");
                                    exceptions = null;
                                    break;
                                }
                                catch (IOException ioException)
                                {
                                    // We do retries if we have an IOException.
                                    // For cases like: `The process cannot access the file 'file path' because
                                    // it is being used by another process`
                                    exceptions ??= new List<Exception>();
                                    exceptions.Add(ioException);
                                    if (attempt < maxAttempts)
                                    {
                                        Thread.Sleep(1000);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger?.Error(ex);
                                    exceptions = null;
                                    break;
                                }
                            }

                            if (exceptions?.Count > 0)
                            {
                                logger?.Error(new AggregateException(exceptions));
                            }
                        }
                    }
                });

            // Add Datadog.Trace dependency to the deps.json
            if (processedDirectories.Count > 0)
            {
                foreach (var directory in processedDirectories)
                {
                    var version = typeof(Instrumentation).Assembly.GetName().Version?.ToString();
                    foreach (var depsJsonPath in Directory.EnumerateFiles(directory, "*.deps.json", SearchOption.TopDirectoryOnly))
                    {
                        if (AssemblyProcessor.IsIgnoredAssemblyDependencyManifest(Path.GetFileName(depsJsonPath)))
                        {
                            logger?.Debug($"Skipping dependency manifest for ignored assembly: {depsJsonPath}");
                            continue;
                        }

                        try
                        {
                            var json = JObject.Parse(File.ReadAllText(depsJsonPath));

                            var propertyName = $"Datadog.Trace/{version}";
                            var isDirty = false;

                            if (json["libraries"] is JObject libraries)
                            {
                                if (!libraries.ContainsKey(propertyName))
                                {
                                    libraries.Add(propertyName, JObject.FromObject(new { type = "reference", serviceable = false, sha512 = string.Empty }));
                                    isDirty = true;
                                }
                            }

                            var targetProperties = json["targets"] is JObject targets
                                                       ? targets.Properties()
                                                       : Array.Empty<JProperty>();
                            foreach (var targetProperty in targetProperties)
                            {
                                var target = (JObject)targetProperty.Value;
                                if (!target.ContainsKey(propertyName))
                                {
                                    target.Add(
                                        propertyName,
                                        new JObject(
                                            new JProperty(
                                                "runtime",
                                                new JObject(
                                                    new JProperty(
                                                        "Datadog.Trace.dll",
                                                        new JObject(
                                                            new JProperty("assemblyVersion", version),
                                                            new JProperty("fileVersion", version)))))));
                                    isDirty = true;
                                }
                            }

                            if (isDirty)
                            {
                                using var stream = File.CreateText(depsJsonPath);
                                using var writer = new JsonTextWriter(stream) { Formatting = Formatting.Indented };
                                json.WriteTo(writer);
                            }

                            logger?.Debug($"Done: {depsJsonPath} [Modified:{isDirty}]");
                        }
                        catch (Exception ex)
                        {
                            logger?.Error(ex, $"Error processing file: {depsJsonPath}");
                        }
                    }
                }
            }

            return numAssemblies;
        }

        internal static bool HasAssemblyExtension(string filePath)
        {
            if (Path.GetDirectoryName(filePath) is { } directory &&
                Path.GetFileName(directory).StartsWith(AssemblyProcessor.StagingDirectoryPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var extension = Path.GetExtension(filePath);
            return extension is "" ||
                   string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);
        }
    }
}
