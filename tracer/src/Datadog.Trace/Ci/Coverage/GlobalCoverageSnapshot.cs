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
    private Action<GlobalCoverageSnapshot>? _disposeCallback;
    private int _outputMasks;
    private int _outputInitialized;
    private int _disposed;

    public GlobalCoverageSnapshot(
        GlobalCoverageInfo model,
        long generationId,
        long mergedContextCount,
        long completenessEpoch,
        SemaphoreSlim snapshotGate,
        Action? releaseAdmission)
    {
        Model = model;
        GenerationId = generationId;
        MergedContextCount = mergedContextCount;
        CompletenessEpoch = completenessEpoch;
        _snapshotGate = snapshotGate;
        _releaseAdmission = releaseAdmission;
    }

    public GlobalCoverageInfo Model { get; }

    public long GenerationId { get; }

    public long MergedContextCount { get; }

    public long CompletenessEpoch { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public byte RequiredOutputMask => (byte)(Volatile.Read(ref _outputMasks) & 0xff);

    public byte CommittedOutputMask => (byte)((Volatile.Read(ref _outputMasks) >> 8) & 0xff);

    public void InitializeOutput(byte requiredOutputMask, Action<GlobalCoverageSnapshot> disposeCallback)
    {
        if (Interlocked.CompareExchange(ref _outputInitialized, 1, 0) != 0)
        {
            ThrowHelper.ThrowInvalidOperationException("The global coverage snapshot output mask was already initialized.");
        }

        Volatile.Write(ref _outputMasks, requiredOutputMask);
        _disposeCallback = disposeCallback;
    }

    public void RecordOutputCommit(byte bit)
    {
        while (true)
        {
            var current = Volatile.Read(ref _outputMasks);
            var next = current | (bit << 8);
            if (Interlocked.CompareExchange(ref _outputMasks, next, current) == current)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        Volatile.Write(ref _disposed, 1);
        try
        {
            Interlocked.Exchange(ref _disposeCallback, null)?.Invoke(this);
        }
        finally
        {
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
}
