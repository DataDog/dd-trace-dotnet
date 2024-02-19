// <copyright file="ManualTestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Proxies;
using Datadog.Trace.Ci.Stubs;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci;

internal class ManualTestModule : ITestModule
{
    private ITestModuleProxy _proxy = NullTestModule.Instance;

    public string Name => _proxy.Name;

    public DateTimeOffset StartTime => _proxy.StartTime;

    public string? Framework => _proxy.Framework;

    public void SetTag(string key, string? value) => _proxy.SetTag(key, value);

    public void SetTag(string key, double? value) => _proxy.SetTag(key, value);

    public void SetErrorInfo(string type, string message, string? callStack) => _proxy.SetErrorInfo(type, message, callStack);

    public void SetErrorInfo(Exception exception) => _proxy.SetErrorInfo(exception);

    public void Close() => _proxy.Close();

    public void Close(TimeSpan? duration) => _proxy.Close(duration);

    public Task CloseAsync() => _proxy.CloseAsync();

    public Task CloseAsync(TimeSpan? duration) => _proxy.CloseAsync(duration);

    public ITestSuite GetOrCreateSuite(string name)
        => new ManualTestSuite(this, _proxy.GetOrCreateSuite(name));

    public ITestSuite GetOrCreateSuite(string name, DateTimeOffset? startDate)
        => new ManualTestSuite(this, _proxy.GetOrCreateSuite(name, startDate));

    [DuckTypeTarget]
    internal void SetAutomatic(object testModule)
        => SetAutomatic(testModule.DuckCast<ITestModuleProxy>());

    internal void SetAutomatic(ITestModuleProxy testModule)
    {
        _proxy = testModule;
    }
}
