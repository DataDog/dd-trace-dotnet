// <copyright file="ProbeStatusPoller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ProbeStatuses
{
    internal class ProbeStatusPoller : IProbeStatusPoller
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeStatusPoller));

        private readonly DiagnosticsSink _diagnosticsSink;
        private readonly TimeSpan _shortPeriod = TimeSpan.FromSeconds(10);
        private readonly TimeSpan _longPeriod = TimeSpan.FromMinutes(60);
        private readonly HashSet<FetchProbeStatus> _probes = new();
        private readonly object _locker = new object();
        private Timer _pollerTimer;
        private bool _isPolling;
        private bool _isRecentlyForcedSchedule;

        private ProbeStatusPoller(DiagnosticsSink diagnosticsSink)
        {
            _diagnosticsSink = diagnosticsSink;
        }

        internal static ProbeStatusPoller Create(DiagnosticsSink diagnosticsSink, DebuggerSettings settings)
        {
            return new ProbeStatusPoller(diagnosticsSink);
        }

        private void PollerCallback(object state)
        {
            if (TryAcquireLock())
            {
                try
                {
                    PausePollerTimer();
                    OnProbeStatusesPoll();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Polling probe status has failed.");
                }
                finally
                {
                    ResumePollerTimer();
                    ReleaseLock();
                }
            }
        }

        private bool TryAcquireLock()
        {
            var lockTaken = false;
            try
            {
                Monitor.TryEnter(_locker, ref lockTaken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Probe Status Poller: Lock acquisition failed.");
            }

            return lockTaken;
        }

        private void ReleaseLock()
        {
            Monitor.Exit(_locker);
        }

        private void PausePollerTimer()
        {
            try
            {
                _pollerTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Probe Status Poller: Pausing the poller has failed.");
            }
        }

        private void ResumePollerTimer()
        {
            try
            {
                var waitPeriod = _isRecentlyForcedSchedule ? _shortPeriod : _longPeriod;
                _pollerTimer?.Change(waitPeriod, waitPeriod);
                _isRecentlyForcedSchedule = false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Probe Status Poller: Resuming the poller has failed.");
            }
        }

        private void OnProbeStatusesPoll()
        {
            if (!_probes.Any())
            {
                return;
            }

            var probesToFetch = _probes.
                                Where(p => p.ShouldFetch())
                               .Select(p => p.ProbeId)
                               .ToArray();

            var probeStatuses = _probes.Where(p => !p.ShouldFetch())
                                       .Select(p => p.ProbeStatus)
                                       .ToList();

            if (probesToFetch.Any())
            {
                probeStatuses.AddRange(DebuggerNativeMethods.GetProbesStatuses(probesToFetch));
            }

            if (!probeStatuses.Any())
            {
                return;
            }

            foreach (var probeStatus in probeStatuses)
            {
                var probeVersion = _probes.SingleOrDefault(p => p.ProbeId == probeStatus.ProbeId)?.ProbeVersion ?? 0;
                // Normalize `INSTRUMENTED` status to `INSTALLED`. The `INSTRUMENTED` status is not recognized by the backend,
                // it was added to satisfy Exception Debugging to better distinguish between RequestReJIT (INSTALLED) and actual instrumentation (INSTRUMENTED).
                var status = probeStatus.Status == Status.INSTRUMENTED ? Status.INSTALLED : probeStatus.Status;
                _diagnosticsSink.AddProbeStatus(probeStatus.ProbeId, status, probeVersion, errorMessage: probeStatus.ErrorMessage);
            }
        }

        public void StartPolling()
        {
            if (_isPolling)
            {
                return;
            }

            lock (_locker)
            {
                if (_isPolling)
                {
                    return;
                }

                _pollerTimer = new Timer(PollerCallback, state: null, dueTime: TimeSpan.Zero, _shortPeriod);
                _isPolling = true;
            }
        }

        public void AddProbes(FetchProbeStatus[] newProbes)
        {
            lock (_locker)
            {
                _probes.UnionWith(newProbes);
                ScheduleNextPollInOneSecond();
            }
        }

        private void ScheduleNextPollInOneSecond()
        {
            lock (_locker)
            {
                if (_isPolling)
                {
                    _pollerTimer?.Change(TimeSpan.FromSeconds(1), _shortPeriod);
                    _isRecentlyForcedSchedule = true;
                }
            }
        }

        public void RemoveProbes(string[] removedProbes)
        {
            lock (_locker)
            {
                _probes.RemoveWhere(p => removedProbes.Contains(p.ProbeId));

                foreach (var rmProbe in removedProbes)
                {
                    _diagnosticsSink.Remove(rmProbe);
                }
            }
        }

        public void UpdateProbes(string[] probeIds, FetchProbeStatus[] newProbeStatuses)
        {
            lock (_locker)
            {
                RemoveProbes(probeIds);
                AddProbes(newProbeStatuses);
            }
        }

        public void UpdateProbe(string probeId, FetchProbeStatus newProbeStatus)
        {
            UpdateProbes(new[] { probeId }, new[] { newProbeStatus });
        }

        /// <summary>
        /// Returns a subset of probeIds from <paramref name="candidateProbeIds" /> that have native representation (e.g either requested rejit or rejitted).
        /// Note that <see cref="Status.EMITTING"/> is taken into account, since EMITTING probes are those that not only have native representation,
        /// but their instrumentation is actively executing.
        /// </summary>
        /// <param name="candidateProbeIds">The set of probes that needs to be checked</param>
        /// <returns>An array of ProbeIds that have native representation.</returns>
        public string[] GetBoundedProbes(string[] candidateProbeIds)
        {
            lock (_locker)
            {
                return _probes
                      .Where(p => (p.ShouldFetch() || p.ProbeStatus.Status == Status.EMITTING) && candidateProbeIds
                                .Contains(p.ProbeId))
                      .Select(p => p.ProbeId)
                      .ToArray();
            }
        }

        public void Dispose()
        {
            _pollerTimer?.Dispose();
        }
    }
}
