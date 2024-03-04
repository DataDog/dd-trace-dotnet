// <copyright file="NullTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Stubs;

internal class NullTestSuite(ITestModule module, string name, DateTimeOffset? startDate) : ITestSuite
{
    public string Name { get; } = name;

    public DateTimeOffset StartTime { get; } = startDate ?? DateTimeOffset.UtcNow;

    public ITestModule Module { get; } = module;

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
        => new NullTest(this, name, null);

    public ITest CreateTest(string name, DateTimeOffset startDate)
        => new NullTest(this, name, startDate);
}
