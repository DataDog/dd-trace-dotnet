// <copyright file="ITestProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;

namespace Datadog.Trace.Ci.Proxies;

/// <summary>
/// A duck-type proxy for Datadog.Trace.Ci.Test
/// This is different to the public <see cref="ITest"/> because we can't use that interface to duck type
/// due to the <see cref="SetParameters"/>, <see cref="SetBenchmarkMetadata"/>, and <see cref="AddBenchmarkData"/>
/// methods, which all take custom types as parameters.
/// </summary>
internal interface ITestProxy
{
    string? Name { get; }

    DateTimeOffset StartTime { get; }

    void SetTag(string key, string? value);

    void SetTag(string key, double? value);

    void SetErrorInfo(string type, string message, string? callStack);

    void SetErrorInfo(Exception exception);

    void SetTestMethodInfo(MethodInfo methodInfo);

    void SetTraits(Dictionary<string, List<string>> traits);

    void SetParameters(Dictionary<string, object>? metadata, Dictionary<string, object>? arguments);

    void SetBenchmarkMetadata(Dictionary<string, object?> values);

    void AddBenchmarkData(BenchmarkMeasureType measureType, string info, int n, double max, double min, double mean, double median, double standardDeviation, double standardError, double kurtosis, double skewness, double p99, double p95, double p90);

    void Close(TestStatus status);

    void Close(TestStatus status, TimeSpan? duration);

    void Close(TestStatus status, TimeSpan? duration, string? skipReason);
}
