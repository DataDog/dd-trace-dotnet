// <copyright file="ITestModuleProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Proxies;

/// <summary>
/// A duck-type proxy for Datadog.Trace.Ci.TestModule
/// This is different to the public <see cref="ITestModule"/> because we
/// need to use the can't use that interface to duck type
/// due to the fact that we can't duck-type the <see cref="GetOrCreateSuite(string)"/>
/// return type as an ITestSuite (needs to be ITestSuiteProxy)
/// </summary>
internal interface ITestModuleProxy
{
    string Name { get; }

    DateTimeOffset StartTime { get; }

    string? Framework { get; }

    void SetTag(string key, string? value);

    void SetTag(string key, double? value);

    void SetErrorInfo(string type, string message, string? callStack);

    void SetErrorInfo(Exception exception);

    void Close();

    void Close(TimeSpan? duration);

    Task CloseAsync();

    Task CloseAsync(TimeSpan? duration);

    ITestSuiteProxy GetOrCreateSuite(string name);

    ITestSuiteProxy GetOrCreateSuite(string name, DateTimeOffset? startDate);
}
