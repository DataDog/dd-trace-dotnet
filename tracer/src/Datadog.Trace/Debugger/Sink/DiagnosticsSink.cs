// <copyright file="DiagnosticsSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Sink
{
    /// <summary>
    /// Stores probe diagnostics and coordinates queued emissions.
    /// </summary>
    /// <remarks>
    /// All sink state is serialized through <c>_locker</c>. A queued message is emitted only when its
    /// generation matches <c>_generations[probeId]</c> at dequeue time. <c>RemoveAfterEmit</c> is a
    /// one-shot marker: after that message is returned, both the diagnostic and generation entries are
    /// dropped. <c>IsQueued</c> and <c>HasBeenReturned</c> mirror queue/output state and must only be
    /// changed while holding <c>_locker</c>. Sink methods must not invoke external callbacks while holding
    /// <c>_locker</c>; callers may already hold their own locks before entering this sink.
    /// </remarks>
    internal sealed class DiagnosticsSink
    {
        private const int QueueLimit = 1000;
        private readonly Dictionary<string, TimedMessage> _diagnostics;
        private readonly Dictionary<string, long> _generations;
        private readonly object _locker = new();

        private readonly Func<string> _serviceNameProvider;
        private readonly int _batchSize;
        private readonly TimeSpan _interval;
        private Queue<TimedMessage> _queue;
        private long _nextGeneration;

        private DiagnosticsSink(Func<string> serviceNameProvider, int batchSize, TimeSpan interval)
        {
            _serviceNameProvider = serviceNameProvider;
            _batchSize = batchSize;
            _interval = interval;

            _diagnostics = new Dictionary<string, TimedMessage>();
            _generations = new Dictionary<string, long>();
            _queue = new Queue<TimedMessage>(QueueLimit);
        }

        public static DiagnosticsSink Create(Func<string> serviceNameProvider, DebuggerSettings settings)
        {
            return new DiagnosticsSink(serviceNameProvider, settings.UploadBatchSize, TimeSpan.FromSeconds(settings.DiagnosticsIntervalSeconds));
        }

        private static bool TryEnqueue(Queue<TimedMessage> queue, TimedMessage timedMessage)
        {
            if (queue.Count >= QueueLimit)
            {
                return false;
            }

            queue.Enqueue(timedMessage);
            return true;
        }

        public void AddProbeStatus(string probeId, Status status, int probeVersion = 0, Exception? exception = null, string? errorMessage = null)
        {
            var serviceName = _serviceNameProvider();
            lock (_locker)
            {
                var shouldSkip =
                    _diagnostics.TryGetValue(probeId, out var current) &&
                    !current.RemoveAfterEmit &&
                    !ShouldOverwrite(
                        oldStatus: current.Message.DebuggerDiagnostics.Diagnostics.Status,
                        newStatus: status,
                        oldVersion: current.Message.DebuggerDiagnostics.Diagnostics.ProbeVersion,
                        newVersion: probeVersion);

                if (shouldSkip)
                {
                    return;
                }

                var next = new ProbeStatus(serviceName, probeId, status, probeVersion, exception, errorMessage);
                var timedMessage = new TimedMessage(
                    message: next,
                    generation: GetNextGeneration(probeId, current),
                    lastEmit: Clock.UtcNow);

                _diagnostics[probeId] = timedMessage;
                // EMITTING must be queued even when the queue is full; rebuild cost is preferred over losing it.
                Enqueue(timedMessage, forceRecreate: status == Status.EMITTING);
            }

            bool ShouldOverwrite(Status oldStatus, Status newStatus, int oldVersion, int newVersion)
            {
                return newStatus == Status.ERROR || oldStatus != newStatus || oldVersion < newVersion;
            }
        }

        private long GetNextGeneration(string probeId, TimedMessage? current)
        {
            if (current?.RemoveAfterEmit != true)
            {
                return GetOrCreateGeneration(probeId);
            }

            return AdvanceGeneration(probeId);
        }

        private long AdvanceGeneration(string probeId)
        {
            var generation = ++_nextGeneration;
            _generations[probeId] = generation;
            return generation;
        }

        private long GetOrCreateGeneration(string probeId)
        {
            if (_generations.TryGetValue(probeId, out var generation))
            {
                return generation;
            }

            generation = ++_nextGeneration;
            _generations[probeId] = generation;
            return generation;
        }

        private bool Enqueue(TimedMessage timedMessage, bool forceRecreate = false)
        {
            // forceRecreate rebuilds a full queue so priority EMITTING messages are kept instead of dropped.
            if (TryEnqueue(_queue, timedMessage))
            {
                timedMessage.IsQueued = true;
                return true;
            }

            // If the queue is no larger than the live diagnostics set, recreation cannot evict stale duplicates.
            if (!forceRecreate && _queue.Count <= _diagnostics.Count)
            {
                return false;
            }

            _queue = RecreateQueue(timedMessage);
            return timedMessage.IsQueued;
        }

        private Queue<TimedMessage> RecreateQueue(TimedMessage? priorityMessage = null)
        {
            // Called under _locker. Rebuild queue ownership in priority order:
            // first the message that forced recreation, then one-shot EMITTING removals, then normal re-emits.
            // The separate passes keep IsQueued reset/restore explicit and avoid starving preserved EMITTING diagnostics.
            var queue = new Queue<TimedMessage>(QueueLimit);
            var now = Clock.UtcNow;
            foreach (var timedMessage in _diagnostics.Values)
            {
                timedMessage.IsQueued = false;
            }

            if (priorityMessage is not null && TryEnqueue(queue, priorityMessage))
            {
                priorityMessage.IsQueued = true;
            }

            List<TimedMessage>? overflowedRemoveAfterEmit = null;
            foreach (var timedMessage in _diagnostics.Values)
            {
                if (ReferenceEquals(timedMessage, priorityMessage) || !timedMessage.RemoveAfterEmit)
                {
                    continue;
                }

                if (!TryEnqueue(queue, timedMessage))
                {
                    overflowedRemoveAfterEmit ??= new List<TimedMessage>();
                    overflowedRemoveAfterEmit.Add(timedMessage);
                    continue;
                }

                timedMessage.IsQueued = true;
            }

            if (overflowedRemoveAfterEmit is not null)
            {
                foreach (var timedMessage in overflowedRemoveAfterEmit)
                {
                    EvictIfStillCurrent(timedMessage);
                }
            }

            foreach (var timedMessage in _diagnostics.Values)
            {
                if (ReferenceEquals(timedMessage, priorityMessage) || timedMessage.RemoveAfterEmit)
                {
                    continue;
                }

                if (!TryEnqueue(queue, timedMessage))
                {
                    break;
                }

                timedMessage.LastEmit = now;
                timedMessage.IsQueued = true;
            }

            return queue;
        }

        private void EvictIfStillCurrent(TimedMessage timedMessage)
        {
            var probeId = timedMessage.Message.DebuggerDiagnostics.Diagnostics.ProbeId;
            if (_diagnostics.TryGetValue(probeId, out var current) && ReferenceEquals(current, timedMessage))
            {
                _diagnostics.Remove(probeId);
                _generations.Remove(probeId);
            }
        }

        public void Remove(string probeId)
        {
            lock (_locker)
            {
                if (!_diagnostics.TryGetValue(probeId, out var current) ||
                    current.Message.DebuggerDiagnostics.Diagnostics.Status != Status.EMITTING)
                {
                    _diagnostics.Remove(probeId);
                    _generations.Remove(probeId);
                    return;
                }

                if (current.HasBeenReturned)
                {
                    _diagnostics.Remove(probeId);
                    _generations.Remove(probeId);
                    return;
                }

                current.RemoveAfterEmit = true;
                // Bump the generation so a same-probe overwrite invalidates this preserved EMITTING at dequeue.
                current.Generation = AdvanceGeneration(probeId);
                if (!current.IsQueued)
                {
                    Enqueue(current, forceRecreate: true);
                }
            }
        }

        public List<ProbeStatus> GetDiagnostics()
        {
            lock (_locker)
            {
                var now = Clock.UtcNow;
                List<TimedMessage>? messagesToReemit = null;
                foreach (var timedMessage in _diagnostics.Values)
                {
                    if (timedMessage.IsQueued || timedMessage.RemoveAfterEmit || !ShouldEmitAgain(timedMessage.LastEmit))
                    {
                        continue;
                    }

                    messagesToReemit ??= new List<TimedMessage>();
                    messagesToReemit.Add(timedMessage);
                }

                if (messagesToReemit is not null)
                {
                    foreach (var timedMessage in messagesToReemit)
                    {
                        if (timedMessage.IsQueued || timedMessage.RemoveAfterEmit || !ShouldEmitAgain(timedMessage.LastEmit))
                        {
                            continue;
                        }

                        if (Enqueue(timedMessage))
                        {
                            timedMessage.LastEmit = now;
                        }
                    }
                }

                var probeStatusList = new List<ProbeStatus>();
                var counter = 0;
                while (_queue.Count > 0 && counter <= _batchSize)
                {
                    var timedMessage = _queue.Dequeue();
                    var probe = timedMessage.Message;
                    var diagnostics = probe.DebuggerDiagnostics.Diagnostics;
                    var hasCurrent = _diagnostics.TryGetValue(diagnostics.ProbeId, out var current);
                    var hasGeneration = _generations.TryGetValue(diagnostics.ProbeId, out var generation);
                    if (hasCurrent &&
                        hasGeneration &&
                        current!.Generation == generation &&
                        timedMessage.Generation == generation)
                    {
                        var isCurrentMessage = ReferenceEquals(timedMessage, current);
                        if (isCurrentMessage)
                        {
                            current.IsQueued = false;
                            current.HasBeenReturned = true;
                        }

                        probeStatusList.Add(probe);
                        if (current.RemoveAfterEmit && isCurrentMessage)
                        {
                            _diagnostics.Remove(diagnostics.ProbeId);
                            _generations.Remove(diagnostics.ProbeId);
                        }
                    }

                    counter++;
                }

                return probeStatusList;

                bool ShouldEmitAgain(DateTime lastEmit)
                {
                    return now - lastEmit >= _interval;
                }
            }
        }

        [TestingOnly]
        internal bool HasGeneration(string probeId)
        {
            lock (_locker)
            {
                return _generations.ContainsKey(probeId);
            }
        }

        public int RemainingCapacity()
        {
            lock (_locker)
            {
                return QueueLimit - _queue.Count;
            }
        }

        /// <summary>
        /// Tracks a probe diagnostic while it is owned by this sink.
        /// The mutable flags are part of the queue/dictionary invariant and must be changed under <see cref="_locker"/>.
        /// </summary>
        private sealed class TimedMessage
        {
            public TimedMessage(ProbeStatus message, long generation, DateTime lastEmit)
            {
                Message = message;
                Generation = generation;
                LastEmit = lastEmit;
            }

            public DateTime LastEmit { get; set; }

            public ProbeStatus Message { get; }

            public long Generation { get; set; }

            public bool IsQueued { get; set; }

            public bool HasBeenReturned { get; set; }

            public bool RemoveAfterEmit { get; set; }
        }
    }
}
