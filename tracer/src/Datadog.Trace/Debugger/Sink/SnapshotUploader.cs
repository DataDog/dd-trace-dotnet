// <copyright file="SnapshotUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Sink
{
    internal class SnapshotUploader : DebuggerUploaderBase, ISnapshotUploader
    {
        private readonly SnapshotSink _snapshotSink;
        private readonly IBatchUploader _snapshotBatchUploader;

        private SnapshotUploader(
            SnapshotSink snapshotSink,
            IBatchUploader snapshotBatchUploader,
            DebuggerSettings settings)
            : base(settings)
        {
            _snapshotBatchUploader = snapshotBatchUploader;
            _snapshotSink = snapshotSink;
        }

        public static SnapshotUploader Create(SnapshotSink snapshotSink, IBatchUploader snapshotBatchUploader, DebuggerSettings settings)
        {
            return new SnapshotUploader(snapshotSink, snapshotBatchUploader, settings);
        }

        protected override async Task Upload()
        {
            var snapshots = _snapshotSink.GetSnapshots();
            if (snapshots.Count > 0)
            {
                await _snapshotBatchUploader.Upload(snapshots).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        protected override int GetRemainingCapacity()
        {
            return _snapshotSink.RemainingCapacity();
        }

        public void Add(string probeId, string snapshot)
        {
            _snapshotSink.Add(probeId, snapshot);
        }
    }
}
