// <copyright file="ManualTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;
using Datadog.Trace.Ci.Proxies;

namespace Datadog.Trace.Ci;

internal class ManualTest(ManualTestSuite suite, ITestProxy automaticTestProxy) : ITest
{
    private readonly ManualTestSuite _suite = suite;
    private readonly ITestProxy _proxy = automaticTestProxy;

    public string? Name => _proxy.Name;

    public DateTimeOffset StartTime => _proxy.StartTime;

    public ITestSuite Suite => _suite;

    public void SetTag(string key, string? value) => _proxy.SetTag(key, value);

    public void SetTag(string key, double? value) => _proxy.SetTag(key, value);

    public void SetErrorInfo(string type, string message, string? callStack) => _proxy.SetErrorInfo(type, message, callStack);

    public void SetErrorInfo(Exception exception) => _proxy.SetErrorInfo(exception);

    public void SetTestMethodInfo(MethodInfo methodInfo) => _proxy.SetTestMethodInfo(methodInfo);

    public void SetTraits(Dictionary<string, List<string>> traits) => _proxy.SetTraits(traits);

    public void SetParameters(TestParameters parameters)
    {
        _proxy.SetParameters(parameters?.Metadata, parameters?.Arguments);
    }

    public void SetBenchmarkMetadata(in BenchmarkHostInfo hostInfo, in BenchmarkJobInfo jobInfo)
    {
        var dictionary = new Dictionary<string, object?>();
        dictionary[nameof(BenchmarkHostInfo.ProcessorName)] = hostInfo.ProcessorName;
        dictionary[nameof(BenchmarkHostInfo.ProcessorCount)] = hostInfo.ProcessorCount;
        dictionary[nameof(BenchmarkHostInfo.PhysicalCoreCount)] = hostInfo.PhysicalCoreCount;
        dictionary[nameof(BenchmarkHostInfo.LogicalCoreCount)] = hostInfo.LogicalCoreCount;
        dictionary[nameof(BenchmarkHostInfo.ProcessorMaxFrequencyHertz)] = hostInfo.ProcessorMaxFrequencyHertz;
        dictionary[nameof(BenchmarkHostInfo.OsVersion)] = hostInfo.OsVersion;
        dictionary[nameof(BenchmarkHostInfo.RuntimeVersion)] = hostInfo.RuntimeVersion;
        dictionary[nameof(BenchmarkHostInfo.ChronometerFrequencyHertz)] = hostInfo.ChronometerFrequencyHertz;
        dictionary[nameof(BenchmarkHostInfo.ChronometerResolution)] = hostInfo.ChronometerResolution;

        dictionary[nameof(BenchmarkJobInfo.Description)] = jobInfo.Description;
        dictionary[nameof(BenchmarkJobInfo.Platform)] = jobInfo.Platform;
        dictionary[nameof(BenchmarkJobInfo.RuntimeName)] = jobInfo.RuntimeName;
        dictionary[nameof(BenchmarkJobInfo.RuntimeMoniker)] = jobInfo.RuntimeMoniker;

        _proxy.SetBenchmarkMetadata(dictionary);
    }

    public void AddBenchmarkData(BenchmarkMeasureType measureType, string info, in BenchmarkDiscreteStats statistics)
    {
        _proxy.AddBenchmarkData(
            measureType,
            info,
            statistics.N,
            statistics.Max,
            statistics.Min,
            statistics.Mean,
            statistics.Median,
            statistics.StandardDeviation,
            statistics.StandardError,
            statistics.Kurtosis,
            statistics.Skewness,
            statistics.P99,
            statistics.P95,
            statistics.P90);
    }

    public void Close(TestStatus status) => _proxy.Close(status);

    public void Close(TestStatus status, TimeSpan? duration) => _proxy.Close(status, duration);

    public void Close(TestStatus status, TimeSpan? duration, string? skipReason)
        => _proxy.Close(status, duration, skipReason);
}
