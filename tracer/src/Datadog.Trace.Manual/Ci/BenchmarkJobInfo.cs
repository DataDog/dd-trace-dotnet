// <copyright file="BenchmarkJobInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci;

// NOTE: This is a direct copy of BenchmarkDiscreteStats in Datadog.Trace
// but as a class instead of a struct to allow reverse duck-typing

/// <summary>
/// Benchmark job info
/// </summary>
public class BenchmarkJobInfo
{
    /// <summary>
    /// Gets or sets job description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets job platform
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// Gets or sets job runtime name
    /// </summary>
    public string? RuntimeName { get; set; }

    /// <summary>
    /// Gets or sets job runtime moniker
    /// </summary>
    public string? RuntimeMoniker { get; set; }
}
