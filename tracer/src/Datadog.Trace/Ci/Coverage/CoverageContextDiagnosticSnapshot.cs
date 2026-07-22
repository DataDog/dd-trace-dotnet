// <copyright file="CoverageContextDiagnosticSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal readonly struct CoverageContextDiagnosticSnapshot
{
    internal CoverageContextDiagnosticSnapshot(long started, long closed, long disposed)
    {
        Started = started;
        Closed = closed;
        Disposed = disposed;
    }

    internal long Started { get; }

    internal long Closed { get; }

    internal long Disposed { get; }
}
