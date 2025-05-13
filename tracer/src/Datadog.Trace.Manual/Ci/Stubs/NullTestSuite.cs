// <copyright file="NullTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Stubs;

internal class NullTestSuite : ITestSuite
{
    public static readonly NullTestSuite Instance = new();

    private NullTestSuite()
    {
    }

    public string Name => "Undefined";

    public DateTimeOffset StartTime => default;

    public ITestModule Module => NullTestModule.Instance;

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

    public void Close()
    {
    }

    public void Close(TimeSpan? duration)
    {
    }

    public ITest CreateTest(string name)
        => NullTest.Instance;

    public ITest CreateTest(string name, DateTimeOffset startDate)
        => NullTest.Instance;
}
