// <copyright file="SnapshotSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Sink
{
    internal sealed class SnapshotSink
    {
        private const int DefaultQueueLimit = 1000;

        private readonly BoundedConcurrentQueue<string> _queue;
        private readonly int _batchSize;
        private readonly int _queueLimit;
        private readonly SnapshotSlicer _snapshotSlicer;

        // Null for products that reuse this sink but are not covered by the Dynamic Instrumentation
        // guardrail metrics. Oversized payloads are still dropped, they are just not reported.
        private readonly MetricTags.DebuggerCaptureEventType? _eventType;

        internal SnapshotSink(int batchSize, SnapshotSlicer snapshotSlicer, MetricTags.DebuggerCaptureEventType? eventType, int queueLimit = DefaultQueueLimit)
        {
            _snapshotSlicer = snapshotSlicer;
            _batchSize = batchSize;
            _queueLimit = queueLimit;
            _eventType = eventType;
            _queue = new BoundedConcurrentQueue<string>(queueLimit);
        }

        public static SnapshotSink Create(DebuggerSettings settings, SnapshotSlicer snapshotSlicer, MetricTags.DebuggerCaptureEventType? eventType)
        {
            return new SnapshotSink(settings.UploadBatchSize, snapshotSlicer, eventType);
        }

        public void Add(string probeId, string snapshot, uint incompleteReasons = 0)
        {
            var sliced = _snapshotSlicer.SliceIfNeeded(probeId, snapshot, ref incompleteReasons);
            if (sliced is null)
            {
                DebuggerGuardrailMetrics.RecordEventsDropped(_eventType, MetricTags.DebuggerEventsDroppedReason.PayloadTooLarge);
                return;
            }

            if (!_queue.TryEnqueue(sliced))
            {
                DebuggerGuardrailMetrics.RecordEventsDropped(_eventType, MetricTags.DebuggerEventsDroppedReason.QueueFull);
                return;
            }

            DebuggerGuardrailMetrics.RecordCaptureIncomplete(_eventType, incompleteReasons);
        }

        public List<string> GetSnapshots()
        {
            var snapshots = new List<string>();
            var counter = 0;
            while (!_queue.IsEmpty && counter < _batchSize)
            {
                if (_queue.TryDequeue(out var snapshot))
                {
                    snapshots.Add(snapshot);
                }

                counter++;
            }

            return snapshots;
        }

        public int RemainingCapacity()
        {
            return _queueLimit - _queue.Count;
        }
    }
}
