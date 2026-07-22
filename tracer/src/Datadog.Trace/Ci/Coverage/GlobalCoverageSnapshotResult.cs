// <copyright file="GlobalCoverageSnapshotResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal readonly struct GlobalCoverageSnapshotResult
{
    private GlobalCoverageSnapshotResult(GlobalCoverageSnapshotStatus status, GlobalCoverageSnapshot? snapshot, GlobalCoverageFailureReason failureReason)
    {
        Status = status;
        Snapshot = snapshot;
        FailureReason = failureReason;
    }

    internal GlobalCoverageSnapshotStatus Status { get; }

    internal GlobalCoverageSnapshot? Snapshot { get; }

    internal GlobalCoverageFailureReason FailureReason { get; }

    internal static GlobalCoverageSnapshotResult Success(GlobalCoverageSnapshot snapshot)
        => new(GlobalCoverageSnapshotStatus.Success, snapshot, GlobalCoverageFailureReason.None);

    internal static GlobalCoverageSnapshotResult Suppressed(GlobalCoverageFailureReason reason)
        => new(GlobalCoverageSnapshotStatus.SuppressedIncomplete, null, reason);
}
