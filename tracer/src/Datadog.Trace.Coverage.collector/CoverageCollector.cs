// <copyright file="CoverageCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Formatting = Datadog.Trace.Vendors.Newtonsoft.Json.Formatting;

// Disable SA1300 Warning due the `collector` lowercase in the namespace
#pragma warning disable SA1300
namespace Datadog.Trace.Coverage.collector
{
    /// <summary>
    /// Datadog coverage collector
    /// </summary>
    [DataCollectorTypeUri("datacollector://Datadog/CoverageCollector/1.0")]
    [DataCollectorFriendlyName("DatadogCoverage")]
    public class CoverageCollector : DataCollector
    {
        private DataCollectorLogger? _logger;
        private DataCollectionEvents? _events;
        private CIVisibilitySettings? _ciVisibilitySettings;
        private DateTime _dateTime = DateTime.Now;
        private string? _tracerHome;

        private static void Copy(string sourceDirectory, string targetDirectory)
        {
            var diSource = new DirectoryInfo(sourceDirectory);
            var diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        private static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            var files = source.GetFiles();
            var subFolders = source.GetDirectories();

            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (var fi in files)
            {
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (var diSourceSubDir in subFolders)
            {
                var nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }

        /// <inheritdoc />
        public override void Initialize(XmlElement configurationElement, DataCollectionEvents events, DataCollectionSink dataSink, DataCollectionLogger logger, DataCollectionEnvironmentContext environmentContext)
        {
            _events = events;
            _logger = new DataCollectorLogger(logger, environmentContext.SessionDataCollectionContext);
            Console.SetOut(_logger.GetTextWriter());

            try
            {
                _ciVisibilitySettings = CIVisibilitySettings.FromDefaultSources();

                // Read the DD_DOTNET_TRACER_HOME environment variable
                _tracerHome = Util.EnvironmentHelpers.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
                if (string.IsNullOrEmpty(_tracerHome) || !Directory.Exists(_tracerHome))
                {
                    _logger.Error("Tracer home (DD_DOTNET_TRACER_HOME environment variable) is not defined or folder doesn't exist, coverage has been disabled.");

                    // By not register a handler to SessionStart and SessionEnd the coverage gets disabled (assemblies are not being processed).
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                _ciVisibilitySettings = null;
            }

            if (_events is not null)
            {
                _events.SessionStart += OnSessionStart;
                _events.SessionEnd += OnSessionEnd;
            }
        }

        private void OnSessionStart(object? sender, SessionStartEventArgs e)
        {
            _logger?.SetContext(e.Context);
            var testSources = e.GetPropertyValue("TestSources");
            if (testSources is string testSourceString)
            {
                // Process folder
                var outputFolder = Path.GetDirectoryName(testSourceString);
                if (outputFolder is not null)
                {
                    BackupFolder(outputFolder);
                    ProcessFolder(outputFolder, SearchOption.TopDirectoryOnly);
                }
            }
            else if (testSources is List<string> testSourcesList)
            {
                // Process folder
                foreach (var source in testSourcesList)
                {
                    var outputFolder = Path.GetDirectoryName(source);
                    if (outputFolder is not null)
                    {
                        BackupFolder(outputFolder);
                        ProcessFolder(outputFolder, SearchOption.TopDirectoryOnly);
                    }
                }
            }
            else
            {
                // Process folder
                BackupFolder(Environment.CurrentDirectory);
                ProcessFolder(Environment.CurrentDirectory, SearchOption.AllDirectories);
            }
        }

        private void OnSessionEnd(object? sender, SessionEndEventArgs e)
        {
            _logger?.SetContext(e.Context);
        }

        private void ProcessFolder(string folder, SearchOption searchOption)
        {
            if (_tracerHome is null)
            {
                return;
            }

            var processedDirectories = new HashSet<string>();
            var numAssemblies = 0;
            var tracerAssemblyName = typeof(Tracer).Assembly.GetName().Name;

            // Process assemblies in parallel.
            Parallel.ForEach(
                Directory.EnumerateFiles(folder, "*.*", searchOption),
                file =>
                {
                    // Skip the Datadog.Trace assembly
                    if (tracerAssemblyName == Path.GetFileNameWithoutExtension(file))
                    {
                        return;
                    }

                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension is ".dll" or ".exe")
                    {
                        try
                        {
                            var asmProcessor = new AssemblyProcessor(file, _tracerHome, _logger, _ciVisibilitySettings);
                            asmProcessor.Process();

                            lock (processedDirectories)
                            {
                                numAssemblies++;
                                processedDirectories.Add(Path.GetDirectoryName(file) ?? string.Empty);
                            }
                        }
                        catch (Datadog.Trace.Ci.Coverage.Exceptions.PdbNotFoundException)
                        {
                            // If the PDB file was not found, we skip the assembly without throwing error.
                        }
                        catch (BadImageFormatException)
                        {
                            // If the Assembly has not the correct format (eg. native dll / exe)
                            // We skip processing the assembly.
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error(ex);
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
                        var json = JObject.Parse(File.ReadAllText(depsJsonPath));
                        var libraries = (JObject)json["libraries"];
                        libraries.Add($"Datadog.Trace/{version}", JObject.FromObject(new { type = "reference", serviceable = false, sha512 = string.Empty }));

                        var targets = (JObject)json["targets"];
                        foreach (var targetProperty in targets.Properties())
                        {
                            var target = (JObject)targetProperty.Value;

                            target.Add(
                                $"Datadog.Trace/{version}",
                                new JObject(
                                    new JProperty(
                                        "runtime",
                                        new JObject(
                                            new JProperty(
                                                "Datadog.Trace.dll",
                                                new JObject(
                                                    new JProperty("assemblyVersion", version),
                                                    new JProperty("fileVersion", version)))))));
                        }

                        using (var stream = File.CreateText(depsJsonPath))
                        {
                            using (var writer = new JsonTextWriter(stream) { Formatting = Formatting.Indented })
                            {
                                json.WriteTo(writer);
                            }
                        }
                    }
                }
            }

            _logger?.Warning($"Processed {numAssemblies} assemblies in folder: {folder}");
        }

        private void BackupFolder(string folder)
        {
            var destinationFolder = Path.Combine(folder, _dateTime.ToString("yyyyMMddHHmmss"));
            _logger?.Debug($"Backup folder: {destinationFolder}");
            Copy(folder, destinationFolder);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (_events != null)
            {
                _events.SessionStart -= OnSessionStart;
                _events.SessionEnd -= OnSessionEnd;
            }

            _events = null;
            base.Dispose(disposing);
        }
    }
}
