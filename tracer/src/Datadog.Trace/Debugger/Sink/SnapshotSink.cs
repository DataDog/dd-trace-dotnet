// <copyright file="SnapshotSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

namespace Datadog.Trace.Debugger.Sink
{
    internal class SnapshotSink : ISnapshotSink
    {
        private const int DefaultQueueLimit = 1000;

        private readonly BoundedConcurrentQueue<string> _queue;
        private readonly int _batchSize;
        private readonly SnapshotSlicer _snapshotSlicer;

        internal SnapshotSink(int batchSize, SnapshotSlicer snapshotSlicer)
        {
            _snapshotSlicer = snapshotSlicer;
            _batchSize = batchSize;
            _queue = new BoundedConcurrentQueue<string>(DefaultQueueLimit);
        }

        public static ISnapshotSink Create(DebuggerSettings settings, SnapshotSlicer snapshotSlicer)
        {
            if (settings.IsSnapshotExplorationTestEnabled)
            {
                return new SnapshotExplorationTestSink(settings.SnapshotExplorationTestReportPath, snapshotSlicer);
            }
            else
            {
                return new SnapshotSink(settings.UploadBatchSize, snapshotSlicer);
            }
        }

        public void Add(string probeId, string snapshot)
        {
            _queue.TryEnqueue(_snapshotSlicer.SliceIfNeeded(probeId, snapshot));
        }

        public IList<string> GetSnapshots()
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
            return DefaultQueueLimit - _queue.Count;
        }

        public void Dispose()
        {
        }
    }
}
