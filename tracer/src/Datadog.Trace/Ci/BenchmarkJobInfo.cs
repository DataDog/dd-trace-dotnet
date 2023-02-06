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

/*
public struct BenchmarkMeasureData
{
    public double? N;
    public double? Max;
    public double? Min;
    public double? Mean;
    public double? Median;
    public double? StdDev;
    public double? StdErr;
    public double? Kurtosis;
    public double? Skewness;
}
*/
/*
span.SetMetric("benchmark.runs", stats.N);
                        span.SetMetric("benchmark.duration.mean", stats.Mean);

                        span.SetMetric("benchmark.statistics.n", stats.N);
                        span.SetMetric("benchmark.statistics.max", stats.Max);
                        span.SetMetric("benchmark.statistics.min", stats.Min);
                        span.SetMetric("benchmark.statistics.mean", stats.Mean);
                        span.SetMetric("benchmark.statistics.median", stats.Median);
                        span.SetMetric("benchmark.statistics.std_dev", stats.StandardDeviation);
                        span.SetMetric("benchmark.statistics.std_err", stats.StandardError);
                        span.SetMetric("benchmark.statistics.kurtosis", stats.Kurtosis);
                        span.SetMetric("benchmark.statistics.skewness", stats.Skewness);

                        if (stats.Percentiles != null)
                        {
                            span.SetMetric("benchmark.statistics.p90", stats.Percentiles.P90);
                            span.SetMetric("benchmark.statistics.p95", stats.Percentiles.P95);
                            span.SetMetric("benchmark.statistics.p99", stats.Percentiles.Percentile(99));
                        }
*/
