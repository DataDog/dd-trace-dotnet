// <copyright file="SnapshotExplorationProbeStatusPoller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ProbeStatuses
{
    internal sealed class SnapshotExplorationProbeStatusPoller : IProbeStatusPoller
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SnapshotExplorationProbeStatusPoller));

        private readonly TimeSpan _pollingPeriod = TimeSpan.FromSeconds(1);
        private readonly HashSet<FetchProbeStatus> _probes = new();
        private readonly SnapshotExplorationProbeStatusReporter _statusReporter;
        private readonly object _locker = new object();
        private volatile Timer? _pollerTimer;
        private volatile bool _isDisposed;
        private volatile bool _isPolling;
        private int _pollInProgress;

        internal SnapshotExplorationProbeStatusPoller(string reportFolderPath)
        {
            _statusReporter = new SnapshotExplorationProbeStatusReporter(reportFolderPath);
        }

        public void StartPolling()
        {
            if (_isPolling || _isDisposed)
            {
                return;
            }

            lock (_locker)
            {
                if (_isPolling || _isDisposed)
                {
                    return;
                }

                _pollerTimer = new Timer(PollerCallback, state: null, dueTime: TimeSpan.Zero, _pollingPeriod);
                _isPolling = true;
            }
        }

        public void AddProbes(FetchProbeStatus[] newProbes)
        {
            if (_isDisposed)
            {
                return;
            }

            var shouldPoll = false;
            lock (_locker)
            {
                if (_isDisposed)
                {
                    return;
                }

                _probes.UnionWith(newProbes);
                shouldPoll = newProbes.Any(p => p.ShouldFetch());
            }

            EmitKnownStatuses(newProbes);
            if (shouldPoll)
            {
                PollProbeStatuses();
            }
        }

        public void RemoveProbes(string[] removedProbes)
        {
            if (_isDisposed)
            {
                return;
            }

            lock (_locker)
            {
                if (_isDisposed)
                {
                    return;
                }

                _probes.RemoveWhere(p => removedProbes.Contains(p.ProbeId));
            }
        }

        public void UpdateProbes(string[] probeIds, FetchProbeStatus[] newProbeStatuses)
        {
            if (_isDisposed)
            {
                return;
            }

            var shouldPoll = false;
            lock (_locker)
            {
                if (_isDisposed)
                {
                    return;
                }

                _probes.RemoveWhere(p => probeIds.Contains(p.ProbeId));
                _probes.UnionWith(newProbeStatuses);
                shouldPoll = newProbeStatuses.Any(p => p.ShouldFetch());
            }

            EmitKnownStatuses(newProbeStatuses);
            if (shouldPoll)
            {
                PollProbeStatuses();
            }
        }

        public void UpdateProbe(string probeId, FetchProbeStatus newProbeStatus)
        {
            UpdateProbes(new[] { probeId }, new[] { newProbeStatus });
        }

        public string[] GetBoundedProbes()
        {
            if (_isDisposed)
            {
                return Array.Empty<string>();
            }

            lock (_locker)
            {
                if (_isDisposed)
                {
                    return Array.Empty<string>();
                }

                return _probes
                      .Where(p => p.ShouldFetch() || p.ProbeStatus.Status == Status.EMITTING)
                      .Select(p => p.ProbeId)
                      .ToArray();
            }
        }

        public void Dispose()
        {
            Timer? timer;
            if (_isDisposed)
            {
                return;
            }

            lock (_locker)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                timer = _pollerTimer;
                _pollerTimer = null;
            }

            timer?.Dispose();
            _statusReporter.Dispose();
        }

        private void PollerCallback(object? state)
        {
            if (_isDisposed)
            {
                return;
            }

            if (TryBeginPoll())
            {
                try
                {
                    PausePollerTimer();
                    PollProbeStatusesCore();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Snapshot exploration probe status polling has failed.");
                }
                finally
                {
                    ResumePollerTimer();
                    EndPoll();
                }
            }
        }

        private void PollProbeStatuses()
        {
            if (!TryBeginPoll())
            {
                return;
            }

            try
            {
                PollProbeStatusesCore();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Snapshot exploration probe status polling has failed.");
            }
            finally
            {
                EndPoll();
            }
        }

        private void PollProbeStatusesCore()
        {
            var probesToFetch = GetProbesToFetch();

            if (probesToFetch.Length == 0)
            {
                return;
            }

            var probeStatuses = DebuggerNativeMethods.GetProbesStatuses(probesToFetch.Select(p => p.Key).ToArray());
            if (probeStatuses == null || probeStatuses.Length == 0)
            {
                return;
            }

            var probeVersions = probesToFetch.ToDictionary(p => p.Key, p => p.Value);
            foreach (var probeStatus in probeStatuses)
            {
                int probeVersion;
                if (!probeVersions.TryGetValue(probeStatus.ProbeId, out probeVersion))
                {
                    probeVersion = 0;
                }

                _statusReporter.Report(probeStatus, probeVersion);
            }
        }

        private KeyValuePair<string, int>[] GetProbesToFetch()
        {
            if (_isDisposed)
            {
                return [];
            }

            lock (_locker)
            {
                if (_isDisposed)
                {
                    return [];
                }

                return _probes
                      .Where(p => p.ShouldFetch())
                      .Select(p => new KeyValuePair<string, int>(p.ProbeId, p.ProbeVersion))
                      .ToArray();
            }
        }

        private void EmitKnownStatuses(FetchProbeStatus[] probes)
        {
            foreach (var probe in probes)
            {
                if (probe.ShouldFetch())
                {
                    continue;
                }

                var status = new PInvoke.ProbeStatus(probe.ProbeId, probe.ProbeStatus.Status, probe.ProbeStatus.ErrorMessage);
                _statusReporter.Report(status, probe.ProbeVersion);
            }
        }

        private bool TryBeginPoll()
        {
            return !_isDisposed && Interlocked.CompareExchange(ref _pollInProgress, 1, 0) == 0;
        }

        private void EndPoll()
        {
            Interlocked.Exchange(ref _pollInProgress, 0);
        }

        private void PausePollerTimer()
        {
            try
            {
                var timer = _pollerTimer;
                if (timer != null && !_isDisposed)
                {
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Snapshot exploration probe status poller: Pausing the poller has failed.");
            }
        }

        private void ResumePollerTimer()
        {
            try
            {
                var timer = _pollerTimer;
                if (timer != null && !_isDisposed)
                {
                    timer.Change(_pollingPeriod, _pollingPeriod);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Snapshot exploration probe status poller: Resuming the poller has failed.");
            }
        }
    }
}
