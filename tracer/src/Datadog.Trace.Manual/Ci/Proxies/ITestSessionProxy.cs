// <copyright file="ITestSessionProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Proxies;

/// <summary>
/// A duck-type proxy for Datadog.Trace.Ci.TestSession
/// This is different to the public <see cref="ITestSession"/> because we
/// need to use the can't use that interface to duck type
/// due to the fact that we can't duck-type the <see cref="CreateModule(string)"/>
/// return type as an ITestModule (needs to be ITestModuleProxy)
/// </summary>
internal interface ITestSessionProxy
{
    string? Command { get; }

    string? WorkingDirectory { get; }

    DateTimeOffset StartTime { get; }

    string? Framework { get; }

    void SetTag(string key, string? value);

    void SetTag(string key, double? value);

    void SetErrorInfo(string type, string message, string? callStack);

    void SetErrorInfo(Exception exception);

    void Close(TestStatus status);

    void Close(TestStatus status, TimeSpan? duration);

    Task CloseAsync(TestStatus status);

    Task CloseAsync(TestStatus status, TimeSpan? duration);

    ITestModuleProxy CreateModule(string name);

    ITestModuleProxy CreateModule(string name, string framework, string frameworkVersion);

    ITestModuleProxy CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate);
}
