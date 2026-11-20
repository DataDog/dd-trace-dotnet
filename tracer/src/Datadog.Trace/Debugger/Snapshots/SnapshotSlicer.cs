// <copyright file="SnapshotSlicer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Metrics;
using DatadogDebugger.Util;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal sealed class SnapshotSlicer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SnapshotSlicer));

        private readonly int _maxDepth;
        private readonly int _maxSnapshotSize;

        internal SnapshotSlicer(int maxDepth, int maxSnapshotSize)
        {
            _maxSnapshotSize = maxSnapshotSize;
            _maxDepth = maxDepth;
        }

        public static SnapshotSlicer Create(DebuggerSettings settings, int maxSnapshotSize = BatchUploader.MaxSinglePayloadSize)
        {
            return new SnapshotSlicer(settings.MaximumDepthOfMembersToCopy, maxSnapshotSize);
        }

        public string? SliceIfNeeded(string probeId, string snapshot, ref uint incompleteReasons)
        {
            string pruned;
            bool inputIsTooLarge;
            try
            {
                pruned = SnapshotPruner.Prune(snapshot, _maxSnapshotSize, _maxDepth, out inputIsTooLarge);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to fit snapshot with probe id {ProbeId} due to exception", probeId);
                return DropIfStillOverCap(probeId, snapshot);
            }

            if (ReferenceEquals(pruned, snapshot))
            {
                return inputIsTooLarge ? DropOversized(probeId) : snapshot;
            }

            if (IsTooLarge(pruned))
            {
                return DropOversized(probeId);
            }

            DebuggerGuardrailMetrics.MarkCaptureIncomplete(ref incompleteReasons, MetricTags.DebuggerCaptureIncompleteReason.PayloadTooLarge);
            return pruned;
        }

        private static string? DropOversized(string probeId)
        {
            Log.Warning("Dropped snapshot with probe id {ProbeId} because it exceeded the maximum payload size", probeId);
            return null;
        }

        private string? DropIfStillOverCap(string probeId, string snapshot)
        {
            if (!IsTooLarge(snapshot))
            {
                return snapshot;
            }

            return DropOversized(probeId);
        }

        private bool IsTooLarge(string snapshot)
        {
            // Char count is a lower bound on UTF-8 byte count. Skip GetByteCount for typical small snapshots.
            if (BatchUploader.IsSinglePayloadTooLarge(snapshot.Length, _maxSnapshotSize))
            {
                return true;
            }

            if (snapshot.Length <= _maxSnapshotSize / 4)
            {
                return false;
            }

            return BatchUploader.IsSinglePayloadTooLarge(Encoding.UTF8.GetByteCount(snapshot), _maxSnapshotSize);
        }
    }
}
