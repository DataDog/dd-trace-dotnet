// <copyright file="IBenchmarkDiscreteStats.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

/// <summary>
/// Reverse duck type for Datadog.Trace.Ci.BenchmarkDiscreteStats in Datadog.Trace.Manual
/// </summary>
internal interface IBenchmarkDiscreteStats
{
    int N { get; }

    double Max { get; }

    double Min { get; }

    double Mean { get; }

    double Median { get; }

    double StandardDeviation { get; }

    double StandardError { get; }

    double Kurtosis { get; }

    double Skewness { get; }

    double P99 { get; }

    double P95 { get; }

    double P90 { get; }
}
