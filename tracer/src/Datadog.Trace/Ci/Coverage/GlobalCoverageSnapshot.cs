// <copyright file="GlobalCoverageSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageSnapshot : IDisposable
{
    private SemaphoreSlim? _snapshotGate;
    private Action? _releaseAdmission;
    private int _disposed;

    public GlobalCoverageSnapshot(
        GlobalCoverageInfo model,
        long mergedContextCount,
        long completenessEpoch,
        SemaphoreSlim snapshotGate,
        Action? releaseAdmission)
    {
        Model = model;
        MergedContextCount = mergedContextCount;
        CompletenessEpoch = completenessEpoch;
        _snapshotGate = snapshotGate;
        _releaseAdmission = releaseAdmission;
    }

    public GlobalCoverageInfo Model { get; }

    public long MergedContextCount { get; }

    public long CompletenessEpoch { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void Dispose()
    {
        Volatile.Write(ref _disposed, 1);
        try
        {
            Interlocked.Exchange(ref _snapshotGate, null)?.Release();
        }
        finally
        {
            Interlocked.Exchange(ref _releaseAdmission, null)?.Invoke();
        }
    }
}
