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
    /// Total heap allocations in bytes
    /// </summary>
    TotalHeapAllocations,

    /// <summary>
    /// Application launch in nanoseconds
    /// </summary>
    ApplicationLaunch
}
