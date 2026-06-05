// <copyright file="SnapshotExplorationProbeStatusReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ProbeStatuses
{
    internal sealed class SnapshotExplorationProbeStatusReporter : IDisposable
    {
        private const string FileName = "SnapshotExplorationProbeStatuses.csv";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SnapshotExplorationProbeStatusReporter>();
        private static readonly char[] CsvEscapeCharacters = { ',', '"', '\n', '\r' };
        private readonly object _lock = new();
        private StreamWriter? _writer;
        private bool _disposed;

        public SnapshotExplorationProbeStatusReporter(string folderPath)
        {
            if (folderPath == null)
            {
                throw new ArgumentNullException(nameof(folderPath));
            }

#if NET6_0_OR_GREATER
            var fileName = Environment.ProcessId + "_" + FileName;
#else
            var fileName = Process.GetCurrentProcess().Id + "_" + FileName;
#endif
            Directory.CreateDirectory(folderPath);
            var fullPath = Path.Combine(folderPath, fileName);
            Log.Information("Snapshot exploration probe status reporter: Creating file at {Path}", fullPath);
            _writer = new StreamWriter(fullPath, append: false);
            _writer.AutoFlush = true;
            _writer.WriteLine("Probe ID,Status,Version,Error");
        }

        public void Report(PInvoke.ProbeStatus probeStatus, int probeVersion)
        {
            lock (_lock)
            {
                if (_disposed || _writer == null)
                {
                    return;
                }

                var status = probeStatus.Status == Status.INSTRUMENTED ? Status.INSTALLED : probeStatus.Status;
                _writer.WriteLine(
                    string.Concat(
                        Csv(probeStatus.ProbeId),
                        ",",
                        status,
                        ",",
                        probeVersion,
                        ",",
                        Csv(probeStatus.ErrorMessage)));
            }
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
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var text = value!;
            return text.IndexOfAny(CsvEscapeCharacters) >= 0
                       ? "\"" + text.Replace("\"", "\"\"") + "\""
                       : text;
        }
    }
}
