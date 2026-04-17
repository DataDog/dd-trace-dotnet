// <copyright file="ExplorationTestProbeTracker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger
{
    /// <summary>
    /// Tracks probe hit counts for snapshot exploration tests and requests probe removal
    /// after a configurable number of snapshots have been captured.
    /// This optimization dramatically reduces serialization overhead by removing probes
    /// once we have sufficient test coverage (multiple parameter variations).
    /// </summary>
    internal static class ExplorationTestProbeTracker
    {
        private const string MaxSnapshotsEnvVar = "DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_MAX_SNAPSHOTS_PER_PROBE";

        /// <summary>
        /// Number of snapshots to capture per probe before removing it.
        /// 50 provides good coverage of parameter variations while keeping runtime reasonable.
        /// Testing showed 10 found 4 bugs - trying 50 to see if more variations catch more edge cases.
        /// </summary>
        private const int DefaultMaxSnapshotsPerProbe = 15;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExplorationTestProbeTracker));

        /// <summary>
        /// Tracks how many snapshots have been captured for each probe.
        /// Key: probeId, Value: hit count
        /// </summary>
        private static readonly ConcurrentDictionary<string, int> ProbeHitCounts = new();

        /// <summary>
        /// Tracks probes that have been requested for removal to avoid duplicate requests.
        /// </summary>
        private static readonly ConcurrentDictionary<string, byte> ProbesRequestedForRemoval = new();

        private static int _maxSnapshotsPerProbe = DefaultMaxSnapshotsPerProbe;

        private static int _isEnabled;

        /// <summary>
        /// Gets a value indicating whether the tracker is enabled (only for exploration tests).
        /// </summary>
        public static bool IsEnabled => Volatile.Read(ref _isEnabled) == 1;

        /// <summary>
        /// Enables the tracker. Should only be called when snapshot exploration tests are active.
        /// </summary>
        public static void Enable()
        {
            if (Interlocked.CompareExchange(ref _isEnabled, 1, 0) == 0)
            {
                _maxSnapshotsPerProbe = ReadMaxSnapshotsPerProbe();
                Log.Information<int>("ExplorationTestProbeTracker enabled with MaxSnapshotsPerProbe={Max}", _maxSnapshotsPerProbe);
            }
        }

        /// <summary>
        /// Records a probe hit and determines if the snapshot should be captured.
        /// </summary>
        /// <param name="probeId">The probe identifier.</param>
        /// <returns>
        /// True if the snapshot should be captured (hit count &lt;= threshold).
        /// False if the probe has exceeded its threshold and should be skipped.
        /// </returns>
        public static bool ShouldCaptureSnapshot(string probeId)
        {
            if (!IsEnabled)
            {
                // Not in exploration test mode - always capture
                return true;
            }

            var hitCount = ProbeHitCounts.AddOrUpdate(probeId, 1, static (_, count) => count + 1);

            if (hitCount <= _maxSnapshotsPerProbe)
            {
                // Still within threshold - capture this snapshot
                return true;
            }

            if (hitCount == _maxSnapshotsPerProbe + 1)
            {
                // First time exceeding threshold - request probe removal
                RequestProbeRemoval(probeId);
            }

            // Exceeded threshold - skip serialization
            return false;
        }

        /// <summary>
        /// Requests the native profiler to remove the probe instrumentation.
        /// Uses the existing DI mechanism for probe removal.
        /// </summary>
        private static void RequestProbeRemoval(string probeId)
        {
            // Use TryAdd to ensure we only request removal once per probe
            if (!ProbesRequestedForRemoval.TryAdd(probeId, 0))
            {
                return;
            }

            Log.Debug(
                "Probe {ProbeId} reached {Max} snapshots, requesting removal to reduce overhead",
                property0: probeId,
                property1: _maxSnapshotsPerProbe);

            try
            {
                // Use the existing native mechanism to remove the probe
                var removeRequest = new NativeRemoveProbeRequest(probeId);
                DebuggerNativeMethods.InstrumentProbes([], [], [], [removeRequest]);

                ExplorationTestMetrics.RecordProbeRemoval();
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "Failed to request probe removal for {ProbeId}", probeId);
            }
        }

        /// <summary>
        /// Gets the current hit count for a probe (for diagnostics/metrics).
        /// </summary>
        public static int GetHitCount(string probeId)
        {
            return ProbeHitCounts.TryGetValue(probeId, out var count) ? count : 0;
        }

        /// <summary>
        /// Gets the total number of probes that have been removed.
        /// </summary>
        public static int GetRemovedProbeCount()
        {
            return ProbesRequestedForRemoval.Count;
        }

        /// <summary>
        /// Resets all state (for testing purposes).
        /// </summary>
        internal static void Reset()
        {
            ProbeHitCounts.Clear();
            ProbesRequestedForRemoval.Clear();
            Volatile.Write(ref _isEnabled, 0);
        }

        private static int ReadMaxSnapshotsPerProbe()
        {
            try
            {
                var raw = System.Environment.GetEnvironmentVariable(MaxSnapshotsEnvVar);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return DefaultMaxSnapshotsPerProbe;
                }

                if (int.TryParse(raw, out var value) && value > 0)
                {
                    return value;
                }

                Log.Warning<string, string, int>(
                    "Invalid {EnvVar} value '{Value}'. Falling back to {Default}.",
                    property0: MaxSnapshotsEnvVar,
                    property1: raw,
                    property2: DefaultMaxSnapshotsPerProbe);
            }
            catch
            {
                // best-effort only; always fall back to default
            }

            return DefaultMaxSnapshotsPerProbe;
        }
    }
}
