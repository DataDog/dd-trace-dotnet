// <copyright file="IBenchmarkDiscreteStats.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

/// <summary>
/// Duck type for Datadog.Trace.Ci.BenchmarkDiscreteStats in Datadog.Trace.Manual
/// </summary>
[DuckType("Datadog.Trace.Ci.BenchmarkDiscreteStats", "Datadog.Trace.Manual")]
internal interface IBenchmarkDiscreteStats
{
    [DuckField]
    int N { get; }

    [DuckField]
    double Max { get; }

    [DuckField]
    double Min { get; }

    [DuckField]
    double Mean { get; }

    [DuckField]
    double Median { get; }

    [DuckField]
    double StandardDeviation { get; }

    [DuckField]
    double StandardError { get; }

    [DuckField]
    double Kurtosis { get; }

    [DuckField]
    double Skewness { get; }

    [DuckField]
    double P99 { get; }

    [DuckField]
    double P95 { get; }

    [DuckField]
    double P90 { get; }
}
