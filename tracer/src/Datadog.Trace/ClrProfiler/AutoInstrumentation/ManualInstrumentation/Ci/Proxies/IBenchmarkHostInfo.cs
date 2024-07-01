// <copyright file="IBenchmarkHostInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

/// <summary>
/// Duck type for Datadog.Trace.Ci.BenchmarkHostInfo in Datadog.Trace.Manual
/// </summary>
[DuckType("Datadog.Trace.Ci.BenchmarkHostInfo", "Datadog.Trace.Manual")]
internal interface IBenchmarkHostInfo
{
    [DuckField]
    string? ProcessorName { get; }

    [DuckField]
    int? ProcessorCount { get; }

    [DuckField]
    int? PhysicalCoreCount { get; }

    [DuckField]
    int? LogicalCoreCount { get; }

    [DuckField]
    double? ProcessorMaxFrequencyHertz { get; }

    [DuckField]
    string? OsVersion { get; }

    [DuckField]
    string? RuntimeVersion { get; }

    [DuckField]
    double? ChronometerFrequencyHertz { get; }

    [DuckField]
    double? ChronometerResolution { get; }
}
