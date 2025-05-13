// <copyright file="IBenchmarkJobInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

/// <summary>
/// Reverse duck type for Datadog.Trace.Ci.BenchmarkJobInfo in Datadog.Trace.Manual
/// </summary>
[DuckType("Datadog.Trace.Ci.BenchmarkJobInfo", "Datadog.Trace.Manual")]
internal interface IBenchmarkJobInfo
{
    [DuckField]
    string? Description { get; }

    [DuckField]
    string? Platform { get; }

    [DuckField]
    string? RuntimeName { get; }

    [DuckField]
    string? RuntimeMoniker { get; }
}
