// <copyright file="GlobalCoverageAccumulator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Util;

namespace Datadog.Trace.Ci.Coverage;

internal enum GlobalCoverageMergeResult
{
    Merged,
    AlreadySuppressed,
    BecameSuppressedIncomplete,
}

internal enum GlobalCoverageSnapshotStatus
{
    Success,
    SuppressedIncomplete,
}

internal sealed class GlobalCoverageAccumulator
{
    private readonly object _mergeGate = new();
    private readonly object _completenessGate = new();
    private readonly SemaphoreSlim _snapshotGate = new(1, 1);
    private readonly GlobalCoverageAccumulatorLimits _limits;
    private Generation? _activeGeneration;
    private long _nextGenerationId;
    private int _suppressed;
    private int _failureReason;
    private long _completenessEpoch;
    private bool _completenessFinalized;

    internal GlobalCoverageAccumulator(GlobalCoverageAccumulatorLimits? limits = null)
    {
        _limits = limits ?? GlobalCoverageAccumulatorLimits.Default;
        _activeGeneration = CreateGeneration();
    }

    private enum SnapshotOwnership
    {
        PreSwap,
        SwapMayHaveOccurred,
        DetachedOwned,
    }

    internal bool IsSuppressed => Volatile.Read(ref _suppressed) != 0;

    internal GlobalCoverageFailureReason FailureReason => (GlobalCoverageFailureReason)Volatile.Read(ref _failureReason);

    private static bool IsRecoverable(Exception exception)
        => exception is OutOfMemoryException or OverflowException or GlobalCoverageLimitException or GlobalCoverageMetadataException;

    private static void SetBit(byte[] bitmap, int zeroBasedLine)
    {
        var byteIndex = zeroBasedLine >> 3;
        bitmap[byteIndex] |= (byte)(128 >> (zeroBasedLine & 7));
    }

    private static GlobalCoverageInfo Materialize(Generation generation)
    {
        var globalCoverage = new GlobalCoverageInfo();
        foreach (var pair in generation.Modules)
        {
            var component = new ComponentCoverageInfo(pair.Key.Name);
            var metadata = pair.Value.Metadata;
            for (var i = 0; i < metadata.Files.Length; i++)
            {
                var fileMetadata = metadata.Files[i];
                component.Files.Add(
                    new FileCoverageInfo(fileMetadata.Path)
                    {
                        ExecutableBitmap = fileMetadata.Bitmap,
                        ExecutedBitmap = pair.Value.ExecutedBitmaps[i]
                    });
            }

            globalCoverage.Components.Add(component);
        }

        return globalCoverage;
    }

    internal GlobalCoverageMergeResult TryMerge(IReadOnlyList<ModuleValue> modules)
    {
        if (IsSuppressed)
        {
            return GlobalCoverageMergeResult.AlreadySuppressed;
        }

        try
        {
            lock (_mergeGate)
            {
                if (IsSuppressed || _activeGeneration is null)
                {
                    return GlobalCoverageMergeResult.AlreadySuppressed;
                }

                MergeIntoGeneration(_activeGeneration, modules);
                _activeGeneration.AcceptedContextCount++;
                return GlobalCoverageMergeResult.Merged;
            }
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            Suppress(GlobalCoverageFailureReason.MergeFailed);
            return GlobalCoverageMergeResult.BecameSuppressedIncomplete;
        }
    }

    internal void Suppress(GlobalCoverageFailureReason reason)
    {
        lock (_completenessGate)
        {
            SuppressUnderCompletenessGate(reason);
        }

        ClearActiveGeneration();
    }

    internal bool TryCommit(GlobalCoverageSnapshot snapshot, Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var committed = false;
        lock (_completenessGate)
        {
            if (!IsSuppressed && !snapshot.IsDisposed && snapshot.CompletenessEpoch == _completenessEpoch)
            {
                try
                {
                    action();
                    committed = true;
                }
                catch (Exception ex)
                {
                    SuppressUnderCompletenessGate(GlobalCoverageFailureReason.OutputCommitFailed);
                    exception = ExceptionDispatchInfo.Capture(ex);
                }
            }
        }

        if (exception is not null)
        {
            ClearActiveGeneration();
            exception.Throw();
        }

        return committed;
    }

    internal bool TryFinalizeCompleteness(Action commitReady)
    {
        var failed = false;
        lock (_completenessGate)
        {
            if (IsSuppressed)
            {
                return false;
            }

            try
            {
                commitReady();
                _completenessFinalized = true;
                return true;
            }
            catch
            {
                SuppressUnderCompletenessGate(GlobalCoverageFailureReason.OutputCommitFailed);
                failed = true;
            }
        }

        if (failed)
        {
            ClearActiveGeneration();
        }

        return false;
    }

    private void ClearActiveGeneration()
    {
        lock (_mergeGate)
        {
            _activeGeneration = null;
        }
    }

    private void SuppressUnderCompletenessGate(GlobalCoverageFailureReason reason)
    {
        if (_completenessFinalized)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _suppressed, 1, 0) == 0)
        {
            Volatile.Write(ref _failureReason, (int)reason);
            _completenessEpoch = checked(_completenessEpoch + 1);
        }
    }

    internal GlobalCoverageSnapshotResult AcquireSnapshot(CoverageContextContainer globalContainer, Action? releaseAdmission = null)
    {
        _snapshotGate.Wait();
        var releaseSnapshotGate = true;
        var ownership = SnapshotOwnership.PreSwap;
        Generation? detached = null;
        try
        {
            if (IsSuppressed)
            {
                return GlobalCoverageSnapshotResult.Suppressed(FailureReason);
            }

            var replacement = CreateGeneration();
            lock (_mergeGate)
            {
                if (IsSuppressed || _activeGeneration is null)
                {
                    return GlobalCoverageSnapshotResult.Suppressed(FailureReason);
                }

                var captured = _activeGeneration;
                ownership = SnapshotOwnership.SwapMayHaveOccurred;
                _activeGeneration = replacement;
                detached = captured;
                ownership = SnapshotOwnership.DetachedOwned;
            }

            MergeIntoGeneration(detached, globalContainer.SnapshotModules(_limits.MaximumModules));
            var model = Materialize(detached);
            _ = model.GetTotalPercentage();

            lock (_completenessGate)
            {
                if (IsSuppressed)
                {
                    return GlobalCoverageSnapshotResult.Suppressed(FailureReason);
                }

                var snapshot = new GlobalCoverageSnapshot(model, detached.Id, detached.AcceptedContextCount, _completenessEpoch, _snapshotGate, releaseAdmission);
                releaseSnapshotGate = false;
                return GlobalCoverageSnapshotResult.Success(snapshot);
            }
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            Suppress(GlobalCoverageFailureReason.SnapshotFailed);
            return GlobalCoverageSnapshotResult.Suppressed(FailureReason);
        }
        catch
        {
            if (ownership != SnapshotOwnership.PreSwap)
            {
                Suppress(GlobalCoverageFailureReason.SnapshotFailed);
            }

            throw;
        }
        finally
        {
            if (releaseSnapshotGate)
            {
                _snapshotGate.Release();
            }
        }
    }

    internal GlobalCoverageAccumulatorSnapshot GetDiagnostics()
    {
        lock (_mergeGate)
        {
            var generation = _activeGeneration;
            return new GlobalCoverageAccumulatorSnapshot(
                generation?.Id ?? 0,
                generation?.RetainedBitmapBytes ?? 0,
                generation?.Modules.Count ?? 0,
                generation?.FileSlotCount ?? 0,
                generation?.AcceptedContextCount ?? 0,
                !IsSuppressed,
                FailureReason);
        }
    }

    private Generation CreateGeneration() => new(Interlocked.Increment(ref _nextGenerationId));

    private unsafe void MergeIntoGeneration(Generation generation, IReadOnlyList<ModuleValue> modules)
    {
        foreach (var moduleValue in modules)
        {
            var metadata = moduleValue.Metadata;
            var expectedRawByteLength = CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata);
            if (moduleValue.AllocatedByteLength != expectedRawByteLength)
            {
                throw new GlobalCoverageMetadataException("A coverage buffer length does not match its metadata.");
            }

            if (!generation.Modules.TryGetValue(moduleValue.Module, out var moduleEntry))
            {
                if (generation.Modules.Count >= _limits.MaximumModules)
                {
                    throw new GlobalCoverageLimitException("The global coverage module limit was exceeded.");
                }

                var newFileSlotCount = checked(generation.FileSlotCount + metadata.Files.Length);
                if (newFileSlotCount > _limits.MaximumFileSlots)
                {
                    throw new GlobalCoverageLimitException("The global coverage file-slot limit was exceeded.");
                }

                moduleEntry = new ModuleEntry(metadata);
                generation.Modules.Add(moduleValue.Module, moduleEntry);
                generation.FileSlotCount = newFileSlotCount;
            }
            else if (!ReferenceEquals(moduleEntry.Metadata, metadata))
            {
                throw new GlobalCoverageMetadataException("The same module was observed with different coverage metadata.");
            }

            var rawPointer = moduleValue.FilesLines;
            if (rawPointer == IntPtr.Zero)
            {
                throw new GlobalCoverageMetadataException("A coverage buffer was disposed before aggregation.");
            }

            for (var fileIndex = 0; fileIndex < metadata.Files.Length; fileIndex++)
            {
                var file = metadata.Files[fileIndex];
                var executedBitmap = moduleEntry.ExecutedBitmaps[fileIndex];
                if (metadata.CoverageMode == 0)
                {
                    var counters = (byte*)rawPointer + file.Offset;
                    for (var lineIndex = 0; lineIndex < file.LastExecutableLine; lineIndex++)
                    {
                        if (counters[lineIndex] > 0)
                        {
                            executedBitmap ??= AllocateBitmap(generation, file.LastExecutableLine);
                            SetBit(executedBitmap, lineIndex);
                        }
                    }
                }
                else
                {
                    var counters = (int*)rawPointer + file.Offset;
                    for (var lineIndex = 0; lineIndex < file.LastExecutableLine; lineIndex++)
                    {
                        if (counters[lineIndex] > 0)
                        {
                            executedBitmap ??= AllocateBitmap(generation, file.LastExecutableLine);
                            SetBit(executedBitmap, lineIndex);
                        }
                    }
                }

                moduleEntry.ExecutedBitmaps[fileIndex] = executedBitmap;
            }
        }
    }

    private byte[] AllocateBitmap(Generation generation, int lineCount)
    {
        var byteLength = FileBitmap.GetSize(lineCount);
        if (byteLength > _limits.MaximumSingleBitmapBytes)
        {
            throw new GlobalCoverageLimitException("A global coverage bitmap exceeds the per-file limit.");
        }

        var retainedBytes = checked(generation.RetainedBitmapBytes + byteLength);
        if (retainedBytes > _limits.MaximumBitmapBytesPerGeneration)
        {
            throw new GlobalCoverageLimitException("The global coverage bitmap budget was exceeded.");
        }

        var bitmap = new byte[byteLength];
        generation.RetainedBitmapBytes = retainedBytes;
        return bitmap;
    }

    private sealed class Generation
    {
        internal Generation(long id)
        {
            Id = id;
        }

        internal long Id { get; }

        internal Dictionary<Module, ModuleEntry> Modules { get; } = new();

        internal int RetainedBitmapBytes { get; set; }

        internal int FileSlotCount { get; set; }

        internal long AcceptedContextCount { get; set; }
    }

    private sealed class ModuleEntry
    {
        internal ModuleEntry(ModuleCoverageMetadata metadata)
        {
            Metadata = metadata;
            ExecutedBitmaps = new byte[metadata.Files.Length][];
        }

        internal ModuleCoverageMetadata Metadata { get; }

        internal byte[]?[] ExecutedBitmaps { get; }
    }
}
