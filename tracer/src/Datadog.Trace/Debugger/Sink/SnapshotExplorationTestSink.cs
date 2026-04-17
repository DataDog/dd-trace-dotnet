// <copyright file="SnapshotExplorationTestSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Sink
{
    internal sealed class SnapshotExplorationTestSink : ISnapshotSink
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SnapshotExplorationTestSink>();
        private readonly SnapshotSlicer _snapshotSlicer;
        private readonly ProbeReportWriter _reportWriter;

        internal SnapshotExplorationTestSink(string reportFolderPath, SnapshotSlicer snapshotSlicer)
        {
            _snapshotSlicer = snapshotSlicer;
            _reportWriter = new ProbeReportWriter(reportFolderPath);
        }

        public void Add(string probeId, string snapshot)
        {
            if (snapshot == null)
            {
                Log.Information("Skip adding snapshot exploration snapshot because snapshot is null");
                return;
            }

            var slicedSnapshot = _snapshotSlicer.SliceIfNeeded(probeId, snapshot!);
            _reportWriter.Enqueue(probeId, slicedSnapshot);
        }

        public IList<string> GetSnapshots()
        {
            return ImmutableList<string>.Empty;
        }

        public int RemainingCapacity()
        {
            return 1000;
        }

        public void Dispose()
        {
            _reportWriter.Dispose();
        }

        private sealed class ProbeReportWriter : IDisposable
        {
            private const string _fileName = "SnapshotExplorationTestReport.csv";
            private readonly string _fullPath;
            private readonly HashSet<string> _probesIds;
            private readonly object _lock = new();
            private StreamWriter? _writer;
            private bool _disposed;

            public ProbeReportWriter(string folderPath)
            {
                if (folderPath == null)
                {
                    throw new ArgumentNullException(nameof(folderPath));
                }

                var fileName = Process.GetCurrentProcess().Id + "_" + _fileName;
                _fullPath = Path.Combine(folderPath, fileName);
                _probesIds = new HashSet<string>();

                // Create file and write header immediately
                Log.Information("ProbeReportWriter: Creating file at {Path}", _fullPath);
                _writer = new StreamWriter(_fullPath, false, Encoding.UTF8);
                _writer.AutoFlush = true; // Flush immediately on every write
                _writer.WriteLine("Probe ID,Type,Method,Is valid");
            }

            internal void Enqueue(string probeId, string snapshot)
            {
                lock (_lock)
                {
                    if (_disposed || _writer == null)
                    {
                        Log.Warning("ProbeReportWriter: Cannot write, writer is disposed");
                        return;
                    }

                    var start = ExplorationTestMetrics.IsEnabled ? Stopwatch.GetTimestamp() : 0;
                    try
                    {
                        if (!_probesIds.Add(probeId))
                        {
                            // Already recorded this probe ID
                            return;
                        }

                        string? typeName;
                        string? methodName;
                        var isValid = TryGetTypeAndMethod(snapshot, out typeName, out methodName);

                        // Always emit 4 columns to match the CSV header.
                        _writer.WriteLine(
                            string.Concat(
                                Csv(probeId),
                                ",",
                                Csv(typeName ?? "N/A"),
                                ",",
                                Csv(methodName ?? "N/A"),
                                ",",
                                (isValid ? "True" : "False")));
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Failed to write snapshot for probeId={ProbeId}", probeId);
                    }
                    finally
                    {
                        if (ExplorationTestMetrics.IsEnabled)
                        {
                            ExplorationTestMetrics.RecordSnapshotSinkWrite(Stopwatch.GetTimestamp() - start);
                        }
                    }
                }
            }

            private static bool TryGetTypeAndMethod(string snapshot, out string? typeName, out string? methodName)
            {
                typeName = null;
                methodName = null;
                try
                {
                    // IMPORTANT: Do not deserialize into Snapshot POCO here.
                    // The snapshot payload may evolve and/or contain partial data, and POCO deserialization failures
                    // would incorrectly mark probes as invalid in the exploration test report.
                    // Instead, extract just the minimal fields we need ("logger.name" and "logger.method")
                    // using a streaming JSON reader for robustness and performance.
                    using var stringReader = new StringReader(snapshot);
                    using var reader = new JsonTextReader(stringReader);

                    while (reader.Read())
                    {
                        if (reader.TokenType != JsonToken.PropertyName)
                        {
                            continue;
                        }

                        if (!string.Equals(reader.Value as string, "logger", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        // Move to logger object
                        if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                        {
                            break;
                        }

                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.EndObject)
                            {
                                break;
                            }

                            if (reader.TokenType != JsonToken.PropertyName)
                            {
                                continue;
                            }

                            var prop = reader.Value as string;
                            if (!reader.Read())
                            {
                                break;
                            }

                            if (string.Equals(prop, "name", StringComparison.Ordinal))
                            {
                                typeName = reader.Value as string;
                            }
                            else if (string.Equals(prop, "method", StringComparison.Ordinal))
                            {
                                methodName = reader.Value as string;
                            }

                            if (!string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(methodName))
                            {
                                return true;
                            }
                        }

                        // We found "logger" but didn't find both required fields
                        break;
                    }

                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private static string Csv(string value)
            {
                // Minimal CSV escaping (commas/quotes/newlines)
                if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                {
                    return "\"" + value.Replace("\"", "\"\"") + "\"";
                }

                return value;
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                    Log.Information("ProbeReportWriter.Dispose: Flushing and closing file.");
                    _writer?.Flush();
                    _writer?.Dispose();
                    _writer = null;
                }
            }
        }
    }
}
