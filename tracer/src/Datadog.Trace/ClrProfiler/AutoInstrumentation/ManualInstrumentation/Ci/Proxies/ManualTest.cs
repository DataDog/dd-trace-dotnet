// <copyright file="ManualTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

internal class ManualTest
{
    private readonly ManualTestSuite _suite;

    public ManualTest(ManualTestSuite suite, Test automatic, Type testType)
    {
        _suite = suite;
        AutomaticTest = automatic;
        Proxy = this.DuckImplement(testType);
    }

    /// <summary>
    /// Gets the reverse-duck-type of this object for manual instrumentation
    /// </summary>
    internal object Proxy { get; }

    public Test AutomaticTest { get; }

    [DuckReverseMethod]
    public string? Name => AutomaticTest.Name;

    [DuckReverseMethod]
    public DateTimeOffset StartTime => AutomaticTest.StartTime;

    [DuckReverseMethod]
    public object Suite => _suite.Proxy;

    [DuckReverseMethod]
    public void SetTag(string key, string? value) => AutomaticTest.SetTag(key, value);

    [DuckReverseMethod]
    public void SetTag(string key, double? value) => AutomaticTest.SetTag(key, value);

    [DuckReverseMethod]
    public void SetErrorInfo(string type, string message, string? callStack) => AutomaticTest.SetErrorInfo(type, message, callStack);

    [DuckReverseMethod]
    public void SetErrorInfo(Exception exception) => AutomaticTest.SetErrorInfo(exception);

    [DuckReverseMethod]
    public void SetTestMethodInfo(MethodInfo methodInfo) => AutomaticTest.SetTestMethodInfo(methodInfo);

    [DuckReverseMethod]
    public void SetTraits(Dictionary<string, List<string>> traits) => AutomaticTest.SetTraits(traits);

    [DuckReverseMethod(ParameterTypeNames = ["Datadog.Trace.Ci.TestParameters, Datadog.Trace.Manual"])]
    public void SetParameters(ITestParameters parameters)
    {
        var testParams = new TestParameters { Arguments = parameters.Arguments, Metadata = parameters.Metadata, };
        AutomaticTest.SetParameters(testParams);
    }

    // TODO: Figure out how to do these in non-horrible ways
    [DuckReverseMethod(ParameterTypeNames = ["Datadog.Trace.Ci.BenchmarkHostInfo, Datadog.Trace.Manual", "Datadog.Trace.Ci.BenchmarkJobInfo, Datadog.Trace.Manual"])]
    public void SetBenchmarkMetadata(IBenchmarkHostInfo hostInfo, IBenchmarkJobInfo jobInfo)
        => AutomaticTest.SetBenchmarkMetadata(Convert(hostInfo), Convert(jobInfo));

    [DuckReverseMethod(ParameterTypeNames = ["Datadog.Trace.Ci.BenchmarkMeasureType, Datadog.Trace.Manual", ClrNames.String, "Datadog.Trace.Ci.BenchmarkDiscreteStats, Datadog.Trace.Manual"])]
    public void AddBenchmarkData(BenchmarkMeasureType measureType, string info, IBenchmarkDiscreteStats statistics)
        => AutomaticTest.AddBenchmarkData(measureType, info, Convert(statistics));

    [DuckReverseMethod]
    public void Close(TestStatus status) => AutomaticTest.Close(status);

    [DuckReverseMethod]
    public void Close(TestStatus status, TimeSpan? duration) => AutomaticTest.Close(status, duration);

    [DuckReverseMethod]
    public void Close(TestStatus status, TimeSpan? duration, string? skipReason)
        => AutomaticTest.Close(status, duration, skipReason);

    private static BenchmarkHostInfo Convert(IBenchmarkHostInfo hostInfo) => new()
    {
        ProcessorName = hostInfo.ProcessorName,
        ProcessorCount = hostInfo.ProcessorCount,
        PhysicalCoreCount = hostInfo.PhysicalCoreCount,
        LogicalCoreCount = hostInfo.LogicalCoreCount,
        ProcessorMaxFrequencyHertz = hostInfo.ProcessorMaxFrequencyHertz,
        OsVersion = hostInfo.OsVersion,
        RuntimeVersion = hostInfo.RuntimeVersion,
        ChronometerFrequencyHertz = hostInfo.ChronometerFrequencyHertz,
        ChronometerResolution = hostInfo.ChronometerResolution,
    };

    private static BenchmarkJobInfo Convert(IBenchmarkJobInfo jobInfo) => new()
    {
        Description = jobInfo.Description,
        Platform = jobInfo.Platform,
        RuntimeName = jobInfo.RuntimeName,
        RuntimeMoniker = jobInfo.RuntimeMoniker,
    };

    private static BenchmarkDiscreteStats Convert(IBenchmarkDiscreteStats stats)
        => new(
            n: stats.N,
            max: stats.Max,
            min: stats.Min,
            mean: stats.Mean,
            median: stats.Median,
            standardDeviation: stats.StandardDeviation,
            standardError: stats.StandardError,
            kurtosis: stats.Kurtosis,
            skewness: stats.Skewness,
            p99: stats.P99,
            p95: stats.P95,
            p90: stats.P90);
}
