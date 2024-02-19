// <copyright file="ITestSuiteProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Proxies;

/// <summary>
/// A duck-type proxy for Datadog.Trace.Ci.TestSuite
/// This is different to the public <see cref="ITestSuite"/> because we
/// need to use the can't use that interface to duck type
/// due to the fact that we can't duck-type the <see cref="CreateTest(string)"/> return type as an ITest (needs to be ITestProxy)
/// </summary>
internal interface ITestSuiteProxy
{
    string Name { get; }

    DateTimeOffset StartTime { get; }

    void SetTag(string key, string? value);

    void SetTag(string key, double? value);

    void SetErrorInfo(string type, string message, string? callStack);

    void SetErrorInfo(Exception exception);

    void Close();

    void Close(TimeSpan? duration);

    // Note this is different from the public API - ITestProxy instead of ITest
    ITestProxy CreateTest(string name);

    ITestProxy CreateTest(string name, DateTimeOffset startDate);
}
