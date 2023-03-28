// <copyright file="BenchmarkDiscreteStats.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;

namespace Datadog.Trace.Ci;

/// <summary>
/// Benchmark measurement discrete stats
/// </summary>
public readonly struct BenchmarkDiscreteStats
{
    /// <summary>
    /// Number of samples
    /// </summary>
    public readonly int N;

    /// <summary>
    /// Max value
    /// </summary>
    public readonly double Max;

    /// <summary>
    /// Min value
    /// </summary>
    public readonly double Min;

    /// <summary>
    /// Mean value
    /// </summary>
    public readonly double Mean;

    /// <summary>
    /// Median value
    /// </summary>
    public readonly double Median;

    /// <summary>
    /// Standard deviation value
    /// </summary>
    public readonly double StandardDeviation;

    /// <summary>
    /// Standard error value
    /// </summary>
    public readonly double StandardError;

    /// <summary>
    /// Kurtosis value
    /// </summary>
    public readonly double Kurtosis;

    /// <summary>
    /// Skewness value
    /// </summary>
    public readonly double Skewness;

    /// <summary>
    /// 99 percentile value
    /// </summary>
    public readonly double P99;

    /// <summary>
    /// 95 percentile value
    /// </summary>
    public readonly double P95;

    /// <summary>
    /// 90 percentile value
    /// </summary>
    public readonly double P90;

    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkDiscreteStats"/> struct.
    /// </summary>
    /// <param name="n">Number of samples</param>
    /// <param name="max">Max value</param>
    /// <param name="min">Min value</param>
    /// <param name="mean">Mean value</param>
    /// <param name="median">Median value</param>
    /// <param name="standardDeviation">Standard deviation value</param>
    /// <param name="standardError">Standard error value</param>
    /// <param name="kurtosis">Kurtosis value</param>
    /// <param name="skewness">Skewness value</param>
    /// <param name="p99">99 percentile value</param>
    /// <param name="p95">95 percentile value</param>
    /// <param name="p90">90 percentile value</param>
    public BenchmarkDiscreteStats(int n, double max, double min, double mean, double median, double standardDeviation, double standardError, double kurtosis, double skewness, double p99, double p95, double p90)
    {
        N = n;
        Max = max;
        Min = min;
        Mean = mean;
        Median = median;
        StandardDeviation = standardDeviation;
        StandardError = standardError;
        Kurtosis = kurtosis;
        Skewness = skewness;
        P99 = p99;
        P95 = p95;
        P90 = p90;
    }

    /// <summary>
    /// Get benchmark discrete stats from an array of doubles
    /// </summary>
    /// <param name="values">Array of doubles</param>
    /// <returns>Benchmark discrete stats instance</returns>
    public static BenchmarkDiscreteStats GetFrom(double[] values)
    {
        if (values is null || values.Length == 0)
        {
            return default;
        }

        values = values.ToArray();
        Array.Sort(values);
        var count = values.Length;
        var halfIndex = count / 2;

        var max = values[count - 1];
        var min = values[0];
        var mean = values.Average();
        var median = count % 2 == 0 ? (values[halfIndex - 1] + values[halfIndex]) / 2d : values[halfIndex];

        double sumOfSquaredDifferences = 0;
        double sumOfCubedDifferences = 0;
        double sumOfFourthPowerDifferences = 0;
        foreach (var number in values)
        {
            sumOfSquaredDifferences += Math.Pow(number - mean, 2);
            sumOfCubedDifferences += Math.Pow(number - mean, 3);
            sumOfFourthPowerDifferences += Math.Pow(number - mean, 4);
        }

        var variance = sumOfSquaredDifferences / count;
        var standardDeviation = Math.Sqrt(variance);
        var standardError = standardDeviation / Math.Sqrt(count);
        var kurtosis = ((sumOfFourthPowerDifferences / count) / Math.Pow(variance, 2)) - 3;
        var skewness = (sumOfCubedDifferences / count) / Math.Pow(standardDeviation, 3);
        var p90 = GetPercentile(90);
        var p95 = GetPercentile(95);
        var p99 = GetPercentile(99);

        return new BenchmarkDiscreteStats(count, max, min, mean, median, standardDeviation, standardError, kurtosis, skewness, p99, p95, p90);

        double GetPercentile(double percentile)
        {
            var index = (int)Math.Round((percentile / 100.0) * (values.Length - 1), 0);
            return values[index];
        }
    }
}
