// <copyright file="BenchmarkJobInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Ci;

/// <summary>
/// Benchmark job info
/// </summary>
public struct BenchmarkJobInfo
{
    /// <summary>
    /// Job description
    /// </summary>
    public string? Description;

    /// <summary>
    /// Job platform
    /// </summary>
    public string? Platform;

    /// <summary>
    /// Job runtime name
    /// </summary>
    public string? RuntimeName;

    /// <summary>
    /// Job runtime moniker
    /// </summary>
    public string? RuntimeMoniker;
}
