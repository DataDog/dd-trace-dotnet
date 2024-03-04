// <copyright file="NullTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;

namespace Datadog.Trace.Ci.Stubs;

internal class NullTest(ITestSuite testSuite, string name, DateTimeOffset? startDate) : ITest
{
    public string? Name { get; } = name;

    public DateTimeOffset StartTime { get; } = startDate ?? DateTimeOffset.UtcNow;

    public ITestSuite Suite { get; } = testSuite;

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
    }

    public void SetBenchmarkMetadata(BenchmarkHostInfo hostInfo, BenchmarkJobInfo jobInfo)
    {
    }

    public void AddBenchmarkData(BenchmarkMeasureType measureType, string info, BenchmarkDiscreteStats statistics)
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
