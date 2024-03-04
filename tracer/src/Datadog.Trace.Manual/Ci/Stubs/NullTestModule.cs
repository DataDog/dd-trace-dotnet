// <copyright file="NullTestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Stubs;

internal class NullTestModule : ITestModule
{
    public static readonly NullTestModule Instance = new();

    private NullTestModule()
    {
    }

    public string Name => "Undefined";

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

    public void Close()
    {
    }

    public void Close(TimeSpan? duration)
    {
    }

    public Task CloseAsync() => Task.CompletedTask;

    public Task CloseAsync(TimeSpan? duration) => Task.CompletedTask;

    public ITestSuite GetOrCreateSuite(string name) => NullTestSuite.Instance;

    public ITestSuite GetOrCreateSuite(string name, DateTimeOffset? startDate) => NullTestSuite.Instance;
}
