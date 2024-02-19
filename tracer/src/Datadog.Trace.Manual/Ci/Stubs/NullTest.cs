// <copyright file="NullTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;
using Datadog.Trace.Ci.Proxies;

namespace Datadog.Trace.Ci.Stubs;

internal class NullTest : ITest, ITestProxy
{
    public static readonly NullTest Instance = new();

    private NullTest()
    {
    }

    public string? Name => "Undefined";

    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

    public ITestSuite Suite => NullTestSuite.Instance;

    public void SetTag(string key, string? value)
    {
    }

    public void SetTag(string key, double? value)
    {
    }

    public void SetErrorInfo(string type, string message, string? callStack)
    {
    }

    public void SetErrorInfo(Exception exception)
    {
    }

    public void SetTestMethodInfo(MethodInfo methodInfo)
    {
    }

    public void SetTraits(Dictionary<string, List<string>> traits)
    {
    }

    public void SetParameters(TestParameters parameters)
    {
        // This shouldn't ever actually be invoked, but it simplifies some APIs!
    }

    public void SetBenchmarkMetadata(in BenchmarkHostInfo hostInfo, in BenchmarkJobInfo jobInfo)
    {
        // This shouldn't ever actually be invoked, but it simplifies some APIs!
    }

    public void AddBenchmarkData(BenchmarkMeasureType measureType, string info, in BenchmarkDiscreteStats statistics)
    {
        // This shouldn't ever actually be invoked, but it simplifies some APIs!
    }

    public void SetParameters(Dictionary<string, object>? metadata, Dictionary<string, object>? arguments)
    {
    }

    public void SetBenchmarkMetadata(Dictionary<string, object?> values)
    {
    }

    public void AddBenchmarkData(BenchmarkMeasureType measureType, string info, int n, double max, double min, double mean, double median, double standardDeviation, double standardError, double kurtosis, double skewness, double p99, double p95, double p90)
    {
    }

    public void Close(TestStatus status)
    {
    }

    public void Close(TestStatus status, TimeSpan? duration)
    {
    }

    public void Close(TestStatus status, TimeSpan? duration, string? skipReason)
    {
    }
}
