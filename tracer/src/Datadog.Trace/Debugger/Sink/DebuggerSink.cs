// <copyright file="DebuggerSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Sink
{
    internal class DebuggerSink : IDebuggerSink
    {
        private const double FreeCapacityLowerThreshold = 0.25;
        private const double FreeCapacityUpperThreshold = 0.75;

        private const int MinFlushInterval = 100;
        private const int MaxFlushInterval = 2000;
        private const int InitialFlushInterval = 1000;
        private const int Capacity = 1000;

        private const int StepSize = 200;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerSink));

        private readonly CancellationTokenSource _cancellationSource;
        private readonly SnapshotSink _snapshotSink;
        private readonly ProbeStatusSink _probeStatusSink;
        private readonly int _uploadFlushInterval;
        private readonly int _initialFlushInterval;
        private readonly BatchUploader _batchUploader;

        private DebuggerSink(SnapshotSink snapshotSink, ProbeStatusSink probeStatusSink, BatchUploader batchUploader, int uploadFlushInterval, int initialFlushInterval)
        {
            _batchUploader = batchUploader;
            _uploadFlushInterval = uploadFlushInterval;
            _initialFlushInterval = initialFlushInterval;

            _probeStatusSink = probeStatusSink;
            _snapshotSink = snapshotSink;

            _cancellationSource = new CancellationTokenSource();
        }

        public static DebuggerSink Create(SnapshotSink snapshotSink, ProbeStatusSink probeStatusSink, BatchUploader batchUploader, DebuggerSettings settings)
        {
            var uploadInterval = settings.UploadFlushIntervalMilliseconds;
            var initialInterval =
                uploadInterval != 0
                    ? Math.Max(MinFlushInterval, Math.Min(uploadInterval, MaxFlushInterval))
                    : InitialFlushInterval;

            return new DebuggerSink(snapshotSink, probeStatusSink, batchUploader, uploadInterval, initialInterval);
        }

        public async Task StartFlushingAsync()
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                var currentInterval = _initialFlushInterval;
                try
                {
                    var snapshots = _snapshotSink.GetSnapshots();
                    if (snapshots.Count > 0)
                    {
                        await _batchUploader.Upload(snapshots).ConfigureAwait(continueOnCapturedContext: false);
                    }

                    var diagnostics = _probeStatusSink.GetDiagnostics();
                    if (diagnostics.Count > 0)
                    {
                        await _batchUploader.Upload(diagnostics.Select(JsonConvert.SerializeObject)).ConfigureAwait(continueOnCapturedContext: false);
                    }
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to upload debugger snapshot and/or diagnostics.");
                }
                finally
                {
                    currentInterval = ReconsiderFlushInterval(currentInterval);
                    await Delay(currentInterval).ConfigureAwait(false);
                }
            }

            async Task Delay(int delay)
            {
                if (_cancellationSource.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delay), _cancellationSource.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // We are shutting down, so don't do anything about it
                }
            }
        }

        private int ReconsiderFlushInterval(int currentInterval)
        {
            if (_uploadFlushInterval != 0)
            {
                return currentInterval;
            }

            var remainingPercent = _snapshotSink.RemainingCapacity() * 1D / Capacity;
            var newInterval = remainingPercent switch
            {
                <= FreeCapacityLowerThreshold => Math.Max(currentInterval - StepSize, MinFlushInterval),
                >= FreeCapacityUpperThreshold => Math.Min(currentInterval + StepSize, MaxFlushInterval),
                _ => currentInterval
            };

            if (newInterval != currentInterval)
            {
                Log.Debug<double, int>("Changing flush interval. Remaining available capacity in upload queue {Remaining}%, new flush interval {NewInterval}ms", remainingPercent * 100, newInterval);
            }

            return newInterval;
        }

        public void AddSnapshot(string probeId, string snapshot)
        {
            _snapshotSink.Add(probeId, snapshot);
        }

        public void AddProbeStatus(string probeId, Status status, Exception exception = null, string errorMessage = null)
        {
            _probeStatusSink.AddProbeStatus(probeId, status, exception, errorMessage);
        }

        public void Dispose()
        {
            _cancellationSource.Cancel();
        }
    }
}
