// <copyright file="BenchmarkHostInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci;

// NOTE: This is a direct copy of BenchmarkHostInfo in Datadog.Trace
// but as a class instead of a struct to allow reverse duck-typing

/// <summary>
/// Benchmark host info
/// </summary>
public class BenchmarkHostInfo
{
    /// <summary>
    /// Gets or sets processor Name
    /// </summary>
    public string? ProcessorName { get; set; }

    /// <summary>
    /// Gets or sets physical processor count
    /// </summary>
    public int? ProcessorCount { get; set; }

    /// <summary>
    /// Gets or sets physical core count
    /// </summary>
    public int? PhysicalCoreCount { get; set; }

    /// <summary>
    ///  Gets or sets logical core count
    /// </summary>
    public int? LogicalCoreCount { get; set; }

    /// <summary>
    /// Gets or sets processor max frequency hertz
    /// </summary>
    public double? ProcessorMaxFrequencyHertz { get; set; }

    /// <summary>
    /// Gets or sets oS Version
    /// </summary>
    public string? OsVersion { get; set; }

    /// <summary>
    /// Gets or sets runtime version
    /// </summary>
    public string? RuntimeVersion { get; set; }

    /// <summary>
    /// Gets or sets chronometer Frequency
    /// </summary>
    public double? ChronometerFrequencyHertz { get; set; }

    /// <summary>
    /// Gets or sets chronometer resolution
    /// </summary>
    public double? ChronometerResolution { get; set; }
}
