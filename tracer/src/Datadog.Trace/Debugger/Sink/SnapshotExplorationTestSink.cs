// <copyright file="SnapshotExplorationTestSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Debugger.Sink
{
    internal sealed class SnapshotExplorationTestSink : ISnapshotSink
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SnapshotExplorationTestSink>();
        private readonly SnapshotSlicer _snapshotSlicer;
        private readonly ProbeReportWriter _reportWriter;

        internal SnapshotExplorationTestSink(string reportFilePath, SnapshotSlicer snapshotSlicer)
        {
            _snapshotSlicer = snapshotSlicer;
            _reportWriter = new ProbeReportWriter(reportFilePath);
        }

        public void Add(string probeId, string snapshot)
        {
            var slicedSnapshot = _snapshotSlicer.SliceIfNeeded(probeId, snapshot);
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
            private readonly string _filePath;
            private readonly BlockingCollection<IdAndSnapshot> _writeQueue;
            private readonly Task _writerTask;
            private readonly CancellationTokenSource _cts;
            private readonly int _bufferSize;
            private bool _disposed;

            public ProbeReportWriter(string filePath, int bufferSize = 4096)
            {
                _filePath = filePath;
                _bufferSize = bufferSize;
                _writeQueue = new BlockingCollection<IdAndSnapshot>();
                _cts = new CancellationTokenSource();
                _writerTask = Task.Run(WriteProcess);
            }

            internal void Enqueue(string probeId, string snapshot)
            {
                try
                {
                    _writeQueue.Add(new IdAndSnapshot(probeId, snapshot));
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to queue snapshot.");
                }
            }

            private void WriteProcess()
            {
                var failures = 0;
                using var writer = new StreamWriter(_filePath, true, Encoding.UTF8, _bufferSize);
                writer.AutoFlush = false;
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var info = _writeQueue.Take(_cts.Token);
                        string? methodFullName = null;
                        if (IsValidJson(info.Snapshot, out var error))
                        {
                            try
                            {
                                var snapshot = JsonConvert.DeserializeObject<Snapshot>(info.Snapshot);
                                methodFullName = $"{snapshot.Logger.Name}.{snapshot.Logger.Method}";
                            }
                            catch (Exception ex)
                            {
                                error = ex.Message;
                            }
                        }

                        var line = $"{info.Id},{methodFullName ?? "N/A"},{string.IsNullOrEmpty(error)}";
                        writer.WriteLine(line);

                        if (writer.BaseStream.Position >= _bufferSize)
                        {
                            writer.Flush();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ThreadAbortException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        if (failures >= 10)
                        {
                            Log.Error(e, "Stopping writing probe report. There was too much errors during the writing process.");
                            throw;
                        }

                        Log.Error(e, "Error writing to probe report file.");
                        failures++;
                    }
                }

                writer.Flush();
            }

            private bool IsValidJson(string jsonString, [NotNullWhen(false)] out string? error)
            {
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    error = "JSON string is null or empty.";
                    return false;
                }

                try
                {
                    JToken.Parse(jsonString);
                    error = null;
                    return true;
                }
                catch (JsonReaderException ex)
                {
                    error = $"Invalid JSON: {ex.Message}";
                    return false;
                }
                catch (Exception ex)
                {
                    error = $"Unexpected error while parsing JSON: {ex.Message}";
                    return false;
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (_disposed)
                {
                    return;
                }

                if (disposing)
                {
                    _cts.Cancel();
                    _writeQueue.CompleteAdding();
                    _writerTask.Wait();
                    _cts.Dispose();
                    _writeQueue.Dispose();
                }

                _disposed = true;
            }
        }

        private record IdAndSnapshot(string Id, string Snapshot);
    }
}
