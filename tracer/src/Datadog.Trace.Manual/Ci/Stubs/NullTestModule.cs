// <copyright file="NullTestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Stubs;

internal class NullTestModule(string name, string? framework, DateTimeOffset? startDate) : ITestModule
{
    public string Name { get; } = name;

    public DateTimeOffset StartTime { get; } = startDate ?? DateTimeOffset.UtcNow;

    public string? Framework { get; } = framework;

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

    public Task CloseAsync() => Task.CompletedTask;

    public Task CloseAsync(TimeSpan? duration) => Task.CompletedTask;

    public ITestSuite GetOrCreateSuite(string name)
        => new NullTestSuite(this, name, null);

    public ITestSuite GetOrCreateSuite(string name, DateTimeOffset? startDate)
        => new NullTestSuite(this, name, startDate);
}
