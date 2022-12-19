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
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ProbeStatuses
{
    internal class ProbeStatusPoller : IProbeStatusPoller
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeStatusPoller));

        private readonly ProbeStatusSink _probeStatusSink;
        private readonly TimeSpan _period;
        private readonly HashSet<string> _probes = new();
        private readonly object _locker = new object();
        private Timer _pollerTimer;
        private bool _isPolling;

        private ProbeStatusPoller(ProbeStatusSink probeStatusSink, TimeSpan period)
        {
            _probeStatusSink = probeStatusSink;
            _period = period;
        }

        internal static ProbeStatusPoller Create(ProbeStatusSink probeStatusSink, DebuggerSettings settings)
        {
            return new ProbeStatusPoller(probeStatusSink, TimeSpan.FromSeconds(settings.DiagnosticsIntervalSeconds));
        }

        private void PollerCallback(object state)
        {
            _pollerTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                OnProbeStatusesPoll();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Polling probe status has failed.");
            }
            finally
            {
                _pollerTimer?.Change(_period, _period);
            }
        }

        private void OnProbeStatusesPoll()
        {
            lock (_locker)
            {
                if (_probes.Count == 0)
                {
                    return;
                }

                var probeStatuses = DebuggerNativeMethods.GetProbesStatuses(_probes.ToArray());

                foreach (var probeStatus in probeStatuses)
                {
                    _probeStatusSink.AddProbeStatus(probeStatus.ProbeId, probeStatus.Status, errorMessage: probeStatus.ErrorMessage);
                }
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

                _pollerTimer = new Timer(PollerCallback, state: null, dueTime: TimeSpan.Zero, _period);
                _isPolling = true;
            }
        }

        public void AddProbes(string[] newProbes)
        {
            lock (_locker)
            {
                _probes.UnionWith(newProbes);
            }
        }

        public void RemoveProbes(string[] removedProbes)
        {
            lock (_locker)
            {
                _probes.ExceptWith(removedProbes);

                foreach (var rmProbe in removedProbes)
                {
                    _probeStatusSink.Remove(rmProbe);
                }
            }
        }

        public void Dispose()
        {
            _pollerTimer?.Dispose();
        }
    }
}
