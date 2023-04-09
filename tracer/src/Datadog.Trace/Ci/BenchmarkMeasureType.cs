// <copyright file="BenchmarkMeasureType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Ci;

/// <summary>
/// Benchmark measure type
/// </summary>
public enum BenchmarkMeasureType
{
    /// <summary>
    /// Duration in nanoseconds
    /// </summary>
    Duration,

    /// <summary>
    /// Run time in nanoseconds
    /// </summary>
    RunTime,

    /// <summary>
    /// Mean heap allocations in bytes
    /// </summary>
    MeanHeapAllocations,

    /// <summary>
    /// Total heap allocations in bytes
    /// </summary>
    TotalHeapAllocations,

    /// <summary>
    /// Application launch in nanoseconds
    /// </summary>
    ApplicationLaunch,

    /// <summary>
    /// Garbage collector gen0 count
    /// </summary>
    GarbageCollectorGen0,

    /// <summary>
    /// Garbage collector gen1 count
    /// </summary>
    GarbageCollectorGen1,

    /// <summary>
    /// Garbage collector gen2 count
    /// </summary>
    GarbageCollectorGen2,

    /// <summary>
    /// Memory total operations count
    /// </summary>
    MemoryTotalOperations,
}
