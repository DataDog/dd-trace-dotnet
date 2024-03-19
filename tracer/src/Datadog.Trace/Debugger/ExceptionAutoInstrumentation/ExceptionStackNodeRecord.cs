// <copyright file="ExceptionStackNodeRecord.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Debugger.Instrumentation.Collections;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

internal class ExceptionStackNodeRecord
{
    public ExceptionStackNodeRecord(int level, TrackedStackFrameNode node)
    {
        Level = level;
        ProbeId = node.ProbeId ?? string.Empty;
        MethodInfo = MethodMetadataCollection.Instance.Get(node.MethodMetadataIndex);
        Snapshot = node.Snapshot;
        SnapshotId = node.SnapshotId;
    }

    public int Level { get; }

    public string ProbeId { get; }

    public MethodMetadataInfo MethodInfo { get; }

    public string Snapshot { get; }

    public string SnapshotId { get; }
}
