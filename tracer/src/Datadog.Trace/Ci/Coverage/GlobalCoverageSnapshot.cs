// <copyright file="GlobalCoverageSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Models.Global;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageSnapshot : IDisposable
{
    private SemaphoreSlim? _snapshotGate;
    private Action? _releaseAdmission;
    private Action<GlobalCoverageSnapshot>? _disposeCallback;
    private int _outputMasks;
    private int _outputInitialized;
    private int _disposed;

    internal GlobalCoverageSnapshot(
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

    internal GlobalCoverageInfo Model { get; }

    internal long GenerationId { get; }

    internal long MergedContextCount { get; }

    internal long CompletenessEpoch { get; }

    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal byte RequiredOutputMask => (byte)(Volatile.Read(ref _outputMasks) & 0xff);

    internal byte CommittedOutputMask => (byte)((Volatile.Read(ref _outputMasks) >> 8) & 0xff);

    internal void InitializeOutput(byte requiredOutputMask, Action<GlobalCoverageSnapshot> disposeCallback)
    {
        if (Interlocked.CompareExchange(ref _outputInitialized, 1, 0) != 0)
        {
            throw new InvalidOperationException("The global coverage snapshot output mask was already initialized.");
        }

        Volatile.Write(ref _outputMasks, requiredOutputMask);
        _disposeCallback = disposeCallback;
    }

    internal void RecordOutputCommit(byte bit)
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
