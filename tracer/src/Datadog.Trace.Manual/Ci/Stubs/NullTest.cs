// <copyright file="NullTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;

namespace Datadog.Trace.Ci.Stubs;

internal class NullTest : ITest
{
    public static readonly NullTest Instance = new();

    private NullTest()
    {
    }

    public string? Name => "Undefined";

    public DateTimeOffset StartTime => default;

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
