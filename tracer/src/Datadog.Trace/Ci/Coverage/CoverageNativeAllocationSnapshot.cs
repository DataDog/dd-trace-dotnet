// <copyright file="CoverageNativeAllocationSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal readonly struct CoverageNativeAllocationSnapshot
{
    internal CoverageNativeAllocationSnapshot(
        long currentBytes,
        long peakBytes,
        long activeBuffers,
        long peakBuffers,
        long allocationCount,
        long freeCount,
        long allocatedBytes,
        long freedBytes,
        long maximumBufferBytes)
    {
        CurrentBytes = currentBytes;
        PeakBytes = peakBytes;
        ActiveBuffers = activeBuffers;
        PeakBuffers = peakBuffers;
        AllocationCount = allocationCount;
        FreeCount = freeCount;
        AllocatedBytes = allocatedBytes;
        FreedBytes = freedBytes;
        MaximumBufferBytes = maximumBufferBytes;
    }

    internal long CurrentBytes { get; }

    internal long PeakBytes { get; }

    internal long ActiveBuffers { get; }

    internal long PeakBuffers { get; }

    internal long AllocationCount { get; }

    internal long FreeCount { get; }

    internal long AllocatedBytes { get; }

    internal long FreedBytes { get; }

    internal long MaximumBufferBytes { get; }
}
