// <copyright file="IBenchmarkHostInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

/// <summary>
/// Reverse duck type for Datadog.Trace.Ci.BenchmarkHostInfo in Datadog.Trace.Manual
/// </summary>
internal interface IBenchmarkHostInfo
{
    string? ProcessorName { get; }

    int? ProcessorCount { get; }

    int? PhysicalCoreCount { get; }

    int? LogicalCoreCount { get; }

    double? ProcessorMaxFrequencyHertz { get; }

    string? OsVersion { get; }

    string? RuntimeVersion { get; }

    double? ChronometerFrequencyHertz { get; }

    double? ChronometerResolution { get; }
}
