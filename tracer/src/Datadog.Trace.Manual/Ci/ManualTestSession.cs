// <copyright file="ManualTestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Proxies;
using Datadog.Trace.Ci.Stubs;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci;

/// <summary>
/// Automatic instrumentation uses reverse duck-typing to create this
/// </summary>
internal class ManualTestSession : ITestSession
{
    private ITestSessionProxy _proxy = NullTestSession.Instance;

    public string? Command => _proxy.Command;

    public string? WorkingDirectory => _proxy.WorkingDirectory;

    public DateTimeOffset StartTime => _proxy.StartTime;

    public string? Framework => _proxy.Framework;

    public void SetTag(string key, string? value) => _proxy.SetTag(key, value);

    public void SetTag(string key, double? value) => _proxy.SetTag(key, value);

    public void SetErrorInfo(string type, string message, string? callStack) => _proxy.SetErrorInfo(type, message, callStack);

    public void SetErrorInfo(Exception exception) => _proxy.SetErrorInfo(exception);

    public void Close(TestStatus status) => _proxy.Close(status);

    public void Close(TestStatus status, TimeSpan? duration) => _proxy.Close(status, duration);

    public Task CloseAsync(TestStatus status) => _proxy.CloseAsync(status);

    public Task CloseAsync(TestStatus status, TimeSpan? duration) => _proxy.CloseAsync(status, duration);

    public ITestModule CreateModule(string name)
    {
        var automaticModule = _proxy.CreateModule(name);
        var manualModule = new ManualTestModule();
        manualModule.SetAutomatic(automaticModule);
        return manualModule;
    }

    public ITestModule CreateModule(string name, string framework, string frameworkVersion)
    {
        var automaticModule = _proxy.CreateModule(name, framework, frameworkVersion);
        var manualModule = new ManualTestModule();
        manualModule.SetAutomatic(automaticModule);
        return manualModule;
    }

    public ITestModule CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
    {
        var automaticModule = _proxy.CreateModule(name, framework, frameworkVersion, startDate);
        var manualModule = new ManualTestModule();
        manualModule.SetAutomatic(automaticModule);
        return manualModule;
    }

    [DuckTypeTarget]
    internal void SetAutomatic(object testSession)
    {
        _proxy = testSession.DuckCast<ITestSessionProxy>();
    }
}
