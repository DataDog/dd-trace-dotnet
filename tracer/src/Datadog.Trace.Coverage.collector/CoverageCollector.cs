// <copyright file="CoverageCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Exceptions;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Formatting = Datadog.Trace.Vendors.Newtonsoft.Json.Formatting;

namespace Datadog.Trace.Coverage.Collector
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
        private string? _tracerHome;
        private int _testNumber;

        /// <inheritdoc />
        public override void Initialize(XmlElement configurationElement, DataCollectionEvents events, DataCollectionSink dataSink, DataCollectionLogger logger, DataCollectionEnvironmentContext environmentContext)
        {
            _events = events;
            _logger = new DataCollectorLogger(logger, environmentContext.SessionDataCollectionContext);

            Initialize();

            if (events is not null)
            {
                events.SessionStart += OnSessionStart;
                events.SessionEnd += OnSessionEnd;
                events.TestHostLaunched += (sender, args) =>
                {
                    _logger?.Debug($"Test host launched with PID: {args.TestHostProcessId} / SessionId: {args.Context?.SessionId?.Id.ToString() ?? "(empty)"}");
                };
                events.TestCaseStart += (sender, args) =>
                {
                    _logger?.Debug($"Test case start [{Interlocked.Increment(ref _testNumber)}]: {args.TestCaseName}");
                };
                events.TestCaseEnd += (sender, args) =>
                {
                    _logger?.Debug($"Test case end: {args.TestCaseName} | Status: {args.TestOutcome}");
                };
            }
        }

        private void OnSessionStart(object? sender, SessionStartEventArgs e)
        {
            _logger?.SetContext(e.Context);
            try
            {
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
            catch (Exception ex)
            {
                if (_logger is { } logger)
                {
                    logger.Error(ex);
                }
                else
                {
                    throw;
                }
            }
        }

        private void OnSessionEnd(object? sender, SessionEndEventArgs e)
        {
            _logger?.SetContext(e.Context);
        }

        internal void Initialize()
        {
            try
            {
                _ciVisibilitySettings = CIVisibilitySettings.FromDefaultSources();

                // Read the DD_DOTNET_TRACER_HOME environment variable
                _tracerHome = Util.EnvironmentHelpers.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
                if (string.IsNullOrEmpty(_tracerHome) || !Directory.Exists(_tracerHome))
                {
                    _logger?.Error("Tracer home (DD_DOTNET_TRACER_HOME environment variable) is not defined or folder doesn't exist, coverage has been disabled.");

                    // By not register a handler to SessionStart and SessionEnd the coverage gets disabled (assemblies are not being processed).
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex);
                _ciVisibilitySettings = null;
            }
        }

        internal void ProcessFolder(string folder, SearchOption searchOption)
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
                    var path = Path.GetDirectoryName(file)!;
                    var fileWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    // Skip the Datadog.Trace assembly
                    if (tracerAssemblyName == fileWithoutExtension)
                    {
                        return;
                    }

                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension is ".dll" or ".exe" or "")
                    {
                        if (File.Exists(Path.Combine(path, fileWithoutExtension + ".pdb")) || File.Exists(Path.Combine(path, fileWithoutExtension + ".PDB")))
                        {
                            List<Exception>? exceptions = null;
                            var remain = 3;
                            Retry:
                            if (--remain > 0)
                            {
                                try
                                {
                                    var asmProcessor = new AssemblyProcessor(file, _tracerHome, _logger, _ciVisibilitySettings);
                                    asmProcessor.Process();
                                    Interlocked.Increment(ref numAssemblies);
                                    if (asmProcessor.HasTracerAssemblyCopied)
                                    {
                                        lock (processedDirectories)
                                        {
                                            processedDirectories.Add(Path.GetDirectoryName(file) ?? string.Empty);
                                        }
                                    }
                                }
                                catch (PdbNotFoundException)
                                {
                                    // If the PDB file was not found, we skip the assembly without throwing error.
                                    _logger?.Debug($"{nameof(PdbNotFoundException)} processing file: {file}");
                                }
                                catch (BadImageFormatException)
                                {
                                    // If the Assembly has not the correct format (eg. native dll / exe)
                                    // We skip processing the assembly.
                                    _logger?.Debug($"{nameof(BadImageFormatException)} processing file: {file}");
                                }
                                catch (IOException ioException)
                                {
                                    // We do retries if we have an IOException.
                                    // For cases like: `The process cannot access the file 'file path' because
                                    // it is being used by another process`
                                    exceptions ??= new List<Exception>();
                                    exceptions.Add(ioException);
                                    Thread.Sleep(1000);
                                    goto Retry;
                                }
                                catch (Exception ex)
                                {
                                    _logger?.Error(ex);
                                }
                            }

                            if (exceptions?.Count > 0)
                            {
                                _logger?.Error(new AggregateException(exceptions));
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

                            _logger?.Debug($"Done: {depsJsonPath} [Modified:{isDirty}]");
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error(ex, $"Error processing file: {depsJsonPath}");
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
