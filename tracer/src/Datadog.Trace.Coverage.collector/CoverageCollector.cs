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

            var numAssemblies = CoverageFolderProcessor.Process(folder, searchOption, _settings, _logger);

            _logger?.Warning($"Processed {numAssemblies} assemblies in folder: {folder}");

            // The following is just a best effort approach to indicate in the test session that
            // we successfully instrumented all assemblies to collect code coverage.
            // Is not part of the spec but useful for support tickets.
            // We try to extract session variables (from out of process sessions)
            // and try to send a message to the IPC server for setting the test.code_coverage.injected tag.
            var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                            EnvironmentHelpers.GetEnvironmentVariables(),
                            new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

            if (extractedContext.SpanContext is { } sessionContext)
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
                _logger?.Debug("CoverageCollector.Test session context cannot be found, skipping IPC client and sending injection tags");
            }
        }

        internal static bool HasAssemblyExtension(string filePath) => CoverageFolderProcessor.HasAssemblyExtension(filePath);

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
