// <copyright file="CoverageNativeAllocationDiagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Ci.Coverage;

internal enum CoverageModuleValueOrigin
{
    TestContext,
    GlobalFallback,
}

internal sealed class CoverageNativeAllocationDiagnostics
{
    internal static readonly CoverageNativeAllocationDiagnostics Process = new();

    private long _testCurrentBytes;
    private long _testPeakBytes;
    private long _testActiveBuffers;
    private long _testPeakBuffers;
    private long _testAllocationCount;
    private long _testFreeCount;
    private long _testAllocatedBytes;
    private long _testFreedBytes;
    private long _testMaximumBufferBytes;
    private long _fallbackCurrentBytes;
    private long _fallbackPeakBytes;
    private long _fallbackActiveBuffers;
    private long _fallbackPeakBuffers;
    private long _fallbackAllocationCount;
    private long _fallbackFreeCount;
    private long _fallbackAllocatedBytes;
    private long _fallbackFreedBytes;
    private long _fallbackMaximumBufferBytes;

    internal void OnAllocated(CoverageModuleValueOrigin origin, int byteLength)
    {
        if (origin == CoverageModuleValueOrigin.TestContext)
        {
            var currentBytes = Interlocked.Add(ref _testCurrentBytes, byteLength);
            var activeBuffers = Interlocked.Increment(ref _testActiveBuffers);
            Interlocked.Increment(ref _testAllocationCount);
            Interlocked.Add(ref _testAllocatedBytes, byteLength);
            SetMaximum(ref _testPeakBytes, currentBytes);
            SetMaximum(ref _testPeakBuffers, activeBuffers);
            SetMaximum(ref _testMaximumBufferBytes, byteLength);
        }
        else
        {
            var currentBytes = Interlocked.Add(ref _fallbackCurrentBytes, byteLength);
            var activeBuffers = Interlocked.Increment(ref _fallbackActiveBuffers);
            Interlocked.Increment(ref _fallbackAllocationCount);
            Interlocked.Add(ref _fallbackAllocatedBytes, byteLength);
            SetMaximum(ref _fallbackPeakBytes, currentBytes);
            SetMaximum(ref _fallbackPeakBuffers, activeBuffers);
            SetMaximum(ref _fallbackMaximumBufferBytes, byteLength);
        }
    }

    internal void OnFreed(CoverageModuleValueOrigin origin, int byteLength)
    {
        if (origin == CoverageModuleValueOrigin.TestContext)
        {
            Interlocked.Add(ref _testCurrentBytes, -byteLength);
            Interlocked.Decrement(ref _testActiveBuffers);
            Interlocked.Increment(ref _testFreeCount);
            Interlocked.Add(ref _testFreedBytes, byteLength);
        }
        else
        {
            Interlocked.Add(ref _fallbackCurrentBytes, -byteLength);
            Interlocked.Decrement(ref _fallbackActiveBuffers);
            Interlocked.Increment(ref _fallbackFreeCount);
            Interlocked.Add(ref _fallbackFreedBytes, byteLength);
        }
    }

    internal CoverageNativeAllocationSnapshot GetSnapshot(CoverageModuleValueOrigin origin)
    {
        return origin == CoverageModuleValueOrigin.TestContext
                   ? new CoverageNativeAllocationSnapshot(
                       Interlocked.Read(ref _testCurrentBytes),
                       Interlocked.Read(ref _testPeakBytes),
                       Interlocked.Read(ref _testActiveBuffers),
                       Interlocked.Read(ref _testPeakBuffers),
                       Interlocked.Read(ref _testAllocationCount),
                       Interlocked.Read(ref _testFreeCount),
                       Interlocked.Read(ref _testAllocatedBytes),
                       Interlocked.Read(ref _testFreedBytes),
                       Interlocked.Read(ref _testMaximumBufferBytes))
                   : new CoverageNativeAllocationSnapshot(
                       Interlocked.Read(ref _fallbackCurrentBytes),
                       Interlocked.Read(ref _fallbackPeakBytes),
                       Interlocked.Read(ref _fallbackActiveBuffers),
                       Interlocked.Read(ref _fallbackPeakBuffers),
                       Interlocked.Read(ref _fallbackAllocationCount),
                       Interlocked.Read(ref _fallbackFreeCount),
                       Interlocked.Read(ref _fallbackAllocatedBytes),
                       Interlocked.Read(ref _fallbackFreedBytes),
                       Interlocked.Read(ref _fallbackMaximumBufferBytes));
    }

    private static void SetMaximum(ref long target, long value)
    {
        var current = Interlocked.Read(ref target);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref target, value, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }
}
