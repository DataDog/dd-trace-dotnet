// <copyright file="SnapshotExplorationConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Debugger;

/// <summary>
/// Constants used by the snapshot-exploration-test setup path. Centralized here
/// to avoid magic strings/numbers scattered across the initialization flow.
/// </summary>
internal static class SnapshotExplorationConstants
{
    /// <summary>
    /// Process / AppDomain name fragment used to detect a vstest test host.
    /// Snapshot-exploration test infrastructure is only initialized when the
    /// current process matches this name to avoid double-initialization in
    /// child processes spawned by tests.
    /// </summary>
    public const string TestHostName = "testhost";

    /// <summary>
    /// File pattern for the native CLR profiler log. The exploration test
    /// setup tails these files to detect when the native side has accepted
    /// the probe configuration.
    /// </summary>
    public const string NativeLogFilePattern = "dotnet-tracer-native-*.log";

    /// <summary>
    /// Substring written by the native profiler when it receives a batch of
    /// probes. Matches the format produced by debugger_rejit_preprocessor.
    /// </summary>
    public const string NativeProbesReceivedMarker = "Dynamic Instrumentation: received";

    /// <summary>
    /// Disambiguator paired with <see cref="NativeProbesReceivedMarker"/> to
    /// confirm the line is the managed-side method-probe acknowledgement.
    /// </summary>
    public const string NativeProbesReceivedSuffix = "method probes from managed side";

    /// <summary>
    /// Maximum time to wait for the native profiler to acknowledge the probe set.
    /// </summary>
    public static readonly TimeSpan ProbeInstallationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum time to wait for managed Dynamic Instrumentation initialization to complete.
    /// </summary>
    public static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Poll interval between reads of the native log file while waiting for
    /// the probes-received marker.
    /// </summary>
    public static readonly TimeSpan NativeLogPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Grace period after the native marker is observed; the marker is logged
    /// before native fully queues the ReJIT requests so we wait briefly to
    /// avoid races at the start of the test run.
    /// </summary>
    public static readonly TimeSpan NativeReadyGracePeriod = TimeSpan.FromMilliseconds(500);
}
