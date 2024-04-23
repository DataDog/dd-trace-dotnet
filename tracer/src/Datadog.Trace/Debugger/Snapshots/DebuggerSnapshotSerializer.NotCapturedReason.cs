// <copyright file="DebuggerSnapshotSerializer.NotCapturedReason.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

#pragma warning disable SA1300

namespace Datadog.Trace.Debugger.Snapshots
{
    internal static partial class DebuggerSnapshotSerializer
    {
        private enum NotCapturedReason
        {
            collectionSize,
            depth,
            fieldCount,
            timeout,
            redactedIdent,
            redactedType
        }
    }
}
