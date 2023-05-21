// <copyright file="DebuggerSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

internal class DebuggerSnapshot
{
    public DebuggerSnapshot(string probeId, string snapshot, Exception exceptionThrown, Guid snapshotId)
    {
        ProbeId = probeId;
        Snapshot = snapshot;
        ExceptionThrown = exceptionThrown;
        SnapshotId = snapshotId;
    }

    public DebuggerSnapshot Child { get; set; }

    public string ProbeId { get; }

    public string Snapshot { get; }

    public Exception ExceptionThrown { get; }

    public Guid SnapshotId { get; }
}
