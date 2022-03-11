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
        private readonly List<AssemblyProcessor> _assemblyProcessors = new();
        private DataCollectorLogger? _logger;
        private DataCollectionEvents? _events;
        private CIVisibilitySettings? _ciVisibilitySettings;

        /// <inheritdoc />
        public override void Initialize(XmlElement configurationElement, DataCollectionEvents events, DataCollectionSink dataSink, DataCollectionLogger logger, DataCollectionEnvironmentContext environmentContext)
        {
            _events = events;
            _logger = new DataCollectorLogger(logger, environmentContext.SessionDataCollectionContext);
            Console.SetOut(_logger.GetTextWriter());

            try
            {
                _ciVisibilitySettings = CIVisibilitySettings.FromDefaultSources();
            }
            catch
            {
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
                        ProcessFolder(outputFolder, SearchOption.TopDirectoryOnly);
                    }
                }
            }
            else
            {
                // Process folder
                ProcessFolder(Environment.CurrentDirectory, SearchOption.AllDirectories);
            }
        }

        private void OnSessionEnd(object? sender, SessionEndEventArgs e)
        {
            _logger?.SetContext(e.Context);
            lock (_assemblyProcessors)
            {
                foreach (var asmProcessor in _assemblyProcessors)
                {
                    asmProcessor.Revert();
                }

                _assemblyProcessors.Clear();
            }
        }

        private void ProcessFolder(string folder, SearchOption searchOption)
        {
            var processedDirectories = new HashSet<string>();
            var numAssemblies = 0;

            // Process assemblies in parallel.
            Parallel.ForEach(
                Directory.EnumerateFiles(folder, "*.*", searchOption),
                file =>
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension is ".dll" or ".exe")
                    {
                        try
                        {
                            var asmProcessor = new AssemblyProcessor(file, _logger);
                            lock (_assemblyProcessors)
                            {
                                _assemblyProcessors.Add(asmProcessor);
                            }

                            asmProcessor.Process();
                            lock (processedDirectories)
                            {
                                numAssemblies++;
                                processedDirectories.Add(Path.GetDirectoryName(file) ?? string.Empty);
                            }
                        }
                        catch (Datadog.Trace.Ci.Coverage.Exceptions.PdbNotFoundException)
                        {
                            // .
                        }
                        catch (BadImageFormatException)
                        {
                            // .
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
