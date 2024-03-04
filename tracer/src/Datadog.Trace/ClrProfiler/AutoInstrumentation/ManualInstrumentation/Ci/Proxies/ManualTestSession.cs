// <copyright file="ManualTestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

internal class ManualTestSession
{
    private readonly Type _moduleType;
    private readonly Type _suiteType;
    private readonly Type _testType;

    public ManualTestSession(TestSession automatic, Type sessionType, Type moduleType, Type suiteType, Type testType)
    {
        _moduleType = moduleType;
        _suiteType = suiteType;
        _testType = testType;
        AutomaticSession = automatic;
        Proxy = this.DuckImplement(sessionType);
    }

    /// <summary>
    /// Gets the reverse-duck-type of this object for manual instrumentation
    /// </summary>
    internal object Proxy { get; }

    public TestSession AutomaticSession { get; }

    [DuckReverseMethod]
    public string? Command => AutomaticSession.Command;

    [DuckReverseMethod]
    public string? WorkingDirectory => AutomaticSession.WorkingDirectory;

    [DuckReverseMethod]
    public DateTimeOffset StartTime => AutomaticSession.StartTime;

    [DuckReverseMethod]
    public string? Framework => AutomaticSession.Framework;

    [DuckReverseMethod]
    public void SetTag(string key, string? value) => AutomaticSession.SetTag(key, value);

    [DuckReverseMethod]
    public void SetTag(string key, double? value) => AutomaticSession.SetTag(key, value);

    [DuckReverseMethod]
    public void SetErrorInfo(string type, string message, string? callStack) => AutomaticSession.SetErrorInfo(type, message, callStack);

    [DuckReverseMethod]
    public void SetErrorInfo(Exception exception) => AutomaticSession.SetErrorInfo(exception);

    [DuckReverseMethod]
    public void Close(TestStatus status) => AutomaticSession.Close(status);

    [DuckReverseMethod]
    public void Close(TestStatus status, TimeSpan? duration) => AutomaticSession.Close(status, duration);

    [DuckReverseMethod]
    public Task CloseAsync(TestStatus status) => AutomaticSession.CloseAsync(status);

    [DuckReverseMethod]
    public Task CloseAsync(TestStatus status, TimeSpan? duration) => AutomaticSession.CloseAsync(status, duration);

    [DuckReverseMethod]
    public object CreateModule(string name)
    {
        // Using the public APIs here because we _want_ to record them in the telemetry etc
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally
        var module = AutomaticSession.CreateModule(name);
#pragma warning restore DD0002
        return new ManualTestModule(module, _moduleType, _suiteType, _testType).Proxy;
    }

    [DuckReverseMethod]
    public object CreateModule(string name, string framework, string frameworkVersion)
    {
        // Using the public APIs here because we _want_ to record them in the telemetry etc
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally
        var module = AutomaticSession.CreateModule(name, framework, frameworkVersion);
#pragma warning restore DD0002
        return new ManualTestModule(module, _moduleType, _suiteType, _testType).Proxy;
    }

    [DuckReverseMethod]
    public object CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
    {
        // Using the public APIs here because we _want_ to record them in the telemetry etc
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally
        var module = AutomaticSession.CreateModule(name, framework, frameworkVersion, startDate);
#pragma warning restore DD0002
        return new ManualTestModule(module, _moduleType, _suiteType, _testType).Proxy;
    }
}
