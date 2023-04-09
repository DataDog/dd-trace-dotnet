// <copyright file="BenchmarkHostInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Ci;

/// <summary>
/// Benchmark host info
/// </summary>
public struct BenchmarkHostInfo
{
    /// <summary>
    /// Processor Name
    /// </summary>
    public string? ProcessorName;

    /// <summary>
    /// Physical processor count
    /// </summary>
    public int? ProcessorCount;

    /// <summary>
    /// Physical core count
    /// </summary>
    public int? PhysicalCoreCount;

    /// <summary>
    ///  Logical core count
    /// </summary>
    public int? LogicalCoreCount;

    /// <summary>
    /// Processor max frequency hertz
    /// </summary>
    public double? ProcessorMaxFrequencyHertz;

    /// <summary>
    /// OS Version
    /// </summary>
    public string? OsVersion;

    /// <summary>
    /// Runtime version
    /// </summary>
    public string? RuntimeVersion;

    /// <summary>
    /// Chronometer Frequency
    /// </summary>
    public double? ChronometerFrequencyHertz;

    /// <summary>
    /// Chronometer resolution
    /// </summary>
    public double? ChronometerResolution;
}
