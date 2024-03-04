// <copyright file="BenchmarkDiscreteStats.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci;

// NOTE: This is a direct copy of BenchmarkDiscreteStats in Datadog.Trace
// but as a class instead of a struct to allow reverse duck-typing

/// <summary>
/// Benchmark measurement discrete stats
/// </summary>
public class BenchmarkDiscreteStats
{
    private static readonly BenchmarkDiscreteStats Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkDiscreteStats"/> class.
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
    /// Gets number of samples
    /// </summary>
    public int N { get; }

    /// <summary>
    /// Gets max value
    /// </summary>
    public double Max { get; }

    /// <summary>
    /// Gets min value
    /// </summary>
    public double Min { get; }

    /// <summary>
    /// Gets mean value
    /// </summary>
    public double Mean { get; }

    /// <summary>
    /// Gets median value
    /// </summary>
    public double Median { get; }

    /// <summary>
    /// Gets standard deviation value
    /// </summary>
    public double StandardDeviation { get; }

    /// <summary>
    /// Gets standard error value
    /// </summary>
    public double StandardError { get; }

    /// <summary>
    /// Gets kurtosis value
    /// </summary>
    public double Kurtosis { get; }

    /// <summary>
    /// Gets skewness value
    /// </summary>
    public double Skewness { get; }

    /// <summary>
    /// Gets 99 percentile value
    /// </summary>
    public double P99 { get; }

    /// <summary>
    /// Gets 95 percentile value
    /// </summary>
    public double P95 { get; }

    /// <summary>
    /// Gets 90 percentile value
    /// </summary>
    public double P90 { get; }

    /// <summary>
    /// Get benchmark discrete stats from an array of doubles
    /// </summary>
    /// <param name="values">Array of doubles</param>
    /// <returns>Benchmark discrete stats instance</returns>
    public static BenchmarkDiscreteStats GetFrom(double[] values)
    {
        if (values is null || values.Length == 0)
        {
            return Empty;
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
