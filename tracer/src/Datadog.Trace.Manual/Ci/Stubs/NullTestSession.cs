// <copyright file="NullTestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Stubs;

internal class NullTestSession(string? command, string? workingDirectory, string? framework, DateTimeOffset? startDate) : ITestSession
{
    public string? Command { get; } = command;

    public string? WorkingDirectory { get; } = workingDirectory;

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

    public void Close(TestStatus status)
    {
    }

    public void Close(TestStatus status, TimeSpan? duration)
    {
    }

    public Task CloseAsync(TestStatus status) => Task.CompletedTask;

    public Task CloseAsync(TestStatus status, TimeSpan? duration) => Task.CompletedTask;

    public ITestModule CreateModule(string name)
        => new NullTestModule(name, null, null);

    public ITestModule CreateModule(string name, string framework, string frameworkVersion)
        => new NullTestModule(name, framework, null);

    public ITestModule CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
        => new NullTestModule(name, framework, startDate);
}
