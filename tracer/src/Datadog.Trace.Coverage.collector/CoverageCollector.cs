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
using Datadog.Trace.Ci.Coverage.Exceptions;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;
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
        private CoverageSettings? _settings;
        private int _testNumber;

        /// <inheritdoc />
        public override void Initialize(XmlElement configurationElement, DataCollectionEvents events, DataCollectionSink dataSink, DataCollectionLogger logger, DataCollectionEnvironmentContext environmentContext)
        {
            _events = events;
            _logger = new DataCollectorLogger(logger, environmentContext.SessionDataCollectionContext);
            if (Initialize(configurationElement) && events is not null)
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

        private bool Initialize(XmlElement configurationElement)
        {
            try
            {
                var coverageSettings = new CoverageSettings(configurationElement);
                if (_logger?.IsDebugEnabled == true)
                {
                    foreach (var item in coverageSettings.ExcludeFilters)
                    {
                        _logger.Debug($"Exclude filter: {item}");
                    }

                    foreach (var item in coverageSettings.ExcludeByAttribute)
                    {
                        _logger.Debug($"Exclude attribute: {item}");
                    }

                    foreach (var item in coverageSettings.ExcludeSourceFiles)
                    {
                        _logger.Debug($"Exclude source: {item}");
                    }
                }

                // Read the DD_DOTNET_TRACER_HOME environment variable
                if (string.IsNullOrEmpty(coverageSettings.TracerHome) || !Directory.Exists(coverageSettings.TracerHome))
                {
                    _logger?.Error("Tracer home (DD_DOTNET_TRACER_HOME environment variable) is not defined or folder doesn't exist, coverage has been disabled.");

                    // By not register a handler to SessionStart and SessionEnd the coverage gets disabled (assemblies are not being processed).
                    return false;
                }

                _settings = coverageSettings;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex);
            }

            return true;
        }

        private void ProcessFolder(string folder, SearchOption searchOption)
        {
            if (_settings is null)
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
                                    var asmProcessor = new AssemblyProcessor(file, _settings, _logger);
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

            // The following is just a best effort approach to indicate in the test session that
            // we sucessfully instrumented all assemblies to collect code coverage.
            // Is not part of the spec but useful for support tickets.
            // We try to extract session variables (from out of process sessions)
            // and try to send a message to the IPC server for setting the test.code_coverage.injected tag.
            if (SpanContextPropagator.Instance.Extract(
                    EnvironmentHelpers.GetEnvironmentVariables(),
                    new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor)) is { } sessionContext)
            {
                try
                {
                    var name = $"session_{sessionContext.SpanId}";
                    _logger?.Debug($"CoverageCollector.Enabling IPC client: {name} and sending injection tags");
                    using var ipcClient = new IpcClient(name);
                    ipcClient.TrySendMessage(new SetSessionTagMessage(CodeCoverageTags.Instrumented, numAssemblies > 0 ? "true" : "false"));
                }
                catch (Exception ex)
                {
                    _logger?.Debug("Error enabling IPC client and sending coverage data: " + ex);
                }
            }
            else
            {
                _logger?.Debug($"CoverageCollector.Test session context cannot be found, skipping IPC client and sending injection tags");
            }
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
