// <copyright file="ManualTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Proxies;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Ci;

internal class ManualTestSuite(ManualTestModule module, ITestSuiteProxy automaticTestSuiteProxy) : ITestSuite
{
    private readonly ManualTestModule _module = module;
    private readonly ITestSuiteProxy _proxy = automaticTestSuiteProxy;

    public string Name => _proxy.Name;

    public DateTimeOffset StartTime => _proxy.StartTime;

    public ITestModule Module => _module;

    public void SetTag(string key, string? value) => _proxy.SetTag(key, value);

    public void SetTag(string key, double? value) => _proxy.SetTag(key, value);

    public void SetErrorInfo(string type, string message, string? callStack) => _proxy.SetErrorInfo(type, message, callStack);

    public void SetErrorInfo(Exception exception) => _proxy.SetErrorInfo(exception);

    public void Close() => _proxy.Close();

    public void Close(TimeSpan? duration) => _proxy.Close(duration);

    public ITest CreateTest(string name)
        => new ManualTest(this, _proxy.CreateTest(name));

    public ITest CreateTest(string name, DateTimeOffset startDate)
        => new ManualTest(this, _proxy.CreateTest(name, startDate));
}
