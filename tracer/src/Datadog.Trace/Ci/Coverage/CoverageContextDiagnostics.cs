// <copyright file="CoverageContextDiagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class CoverageContextDiagnostics
{
    private long _started;
    private long _closed;
    private long _disposed;

    internal void RecordStarted() => Interlocked.Increment(ref _started);

    internal void RecordClosed() => Interlocked.Increment(ref _closed);

    internal void RecordDisposed() => Interlocked.Increment(ref _disposed);

    internal CoverageContextDiagnosticSnapshot GetSnapshot()
        => new(Interlocked.Read(ref _started), Interlocked.Read(ref _closed), Interlocked.Read(ref _disposed));
}
