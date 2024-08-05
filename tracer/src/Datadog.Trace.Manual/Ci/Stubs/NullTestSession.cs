// <copyright file="NullTestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Stubs;

internal class NullTestSession : ITestSession
{
    public static readonly NullTestSession Instance = new();

    public string? Command => null;

    public string? WorkingDirectory => null;

    public DateTimeOffset StartTime => default;

    public string? Framework => null;

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

    ITestModule ITestSession.CreateModule(string name)
        => NullTestModule.Instance;

    ITestModule ITestSession.CreateModule(string name, string framework, string frameworkVersion)
        => NullTestModule.Instance;

    ITestModule ITestSession.CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
        => NullTestModule.Instance;
}
