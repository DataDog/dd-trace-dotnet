// <copyright file="ProbeStatusSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Sink
{
    internal class ProbeStatusSink
    {
        private const int QueueLimit = 1000;
        private readonly ConcurrentDictionary<string, TimedMessage> _diagnostics;

        private readonly string _serviceName;
        private readonly int _batchSize;
        private readonly TimeSpan _interval;

        private BoundedConcurrentQueue<ProbeStatus> _queue;

        protected ProbeStatusSink()
        {
        }

        protected ProbeStatusSink(string serviceName, int batchSize, TimeSpan interval)
        {
            _serviceName = serviceName;
            _batchSize = batchSize;
            _interval = interval;

            _diagnostics = new ConcurrentDictionary<string, TimedMessage>();
            _queue = new BoundedConcurrentQueue<ProbeStatus>(QueueLimit);
        }

        public static ProbeStatusSink Create(string serviceName, DebuggerSettings settings)
        {
            return new ProbeStatusSink(serviceName, settings.UploadBatchSize, TimeSpan.FromSeconds(settings.DiagnosticsIntervalSeconds));
        }

        internal virtual void AddReceived(string probeId)
        {
            AddProbeStatus(probeId, Status.RECEIVED);
        }

        internal virtual void AddInstalled(string probeId)
        {
            AddProbeStatus(probeId, Status.INSTALLED);
        }

        internal virtual void AddBlocked(string probeId)
        {
            AddProbeStatus(probeId, Status.BLOCKED);
        }

        internal virtual void AddError(string probeId, Exception e)
        {
            AddProbeStatus(probeId, Status.ERROR, exception: e);
        }

        public virtual void AddProbeStatus(string probeId, Status status, int probeVersion = 0, Exception exception = null, string errorMessage = null)
        {
            var shouldSkip =
                _diagnostics.TryGetValue(probeId, out var current) &&
                !ShouldOverwrite(
                    oldStatus: current.Message.DebuggerDiagnostics.Diagnostics.Status,
                    newStatus: status,
                    oldVersion: current.Message.DebuggerDiagnostics.Diagnostics.ProbeVersion,
                    newVersion: probeVersion);

            if (shouldSkip)
            {
                return;
            }

            var next = new ProbeStatus(_serviceName, probeId, status, probeVersion, exception, errorMessage);
            var timedMessage = new TimedMessage
            {
                LastEmit = Clock.UtcNow,
                Message = next
            };

            _diagnostics.AddOrUpdate(probeId, timedMessage, (_, _) => (timedMessage));
            Enqueue(next);

            bool ShouldOverwrite(Status oldStatus, Status newStatus, int oldVersion, int newVersion)
            {
                return newStatus == Status.ERROR || oldStatus != newStatus || oldVersion < newVersion;
            }
        }

        private void Enqueue(ProbeStatus probe)
        {
            if (_queue.TryEnqueue(probe))
            {
                return;
            }

            if (_queue.Count <= _diagnostics.Count)
            {
                return;
            }

            _queue = RecreateQueue();
        }

        private BoundedConcurrentQueue<ProbeStatus> RecreateQueue()
        {
            var queue = new BoundedConcurrentQueue<ProbeStatus>(QueueLimit);
            var now = Clock.UtcNow;
            foreach (var timedMessage in _diagnostics.Values)
            {
                timedMessage.LastEmit = now;
                if (!queue.TryEnqueue(timedMessage.Message))
                {
                    break;
                }
            }

            return queue;
        }

        public virtual void Remove(string probeId)
        {
            _diagnostics.TryRemove(probeId, out _);
        }

        public virtual List<ProbeStatus> GetDiagnostics()
        {
            var now = Clock.UtcNow;
            foreach (var timedMessage in _diagnostics.Values)
            {
                if (!ShouldEmitAgain(timedMessage.LastEmit))
                {
                    continue;
                }

                timedMessage.LastEmit = now;
                Enqueue(timedMessage.Message);
            }

            var probeStatusList = new List<ProbeStatus>();
            var counter = 0;
            while (!_queue.IsEmpty && counter <= _batchSize)
            {
                if (_queue.TryDequeue(out var probe))
                {
                    if (_diagnostics.ContainsKey(probe.DebuggerDiagnostics.Diagnostics.ProbeId))
                    {
                        probeStatusList.Add(probe);
                    }
                }

                counter++;
            }

            return probeStatusList;

            bool ShouldEmitAgain(DateTime latEmit)
            {
                return now - latEmit >= _interval;
            }
        }
    }
}
