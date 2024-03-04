// <copyright file="ManualTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

internal class ManualTestSuite
{
    private readonly ManualTestModule _module;
    private readonly Type _testType;

    public ManualTestSuite(ManualTestModule module, TestSuite automatic, Type suiteType, Type testType)
    {
        _module = module;
        _testType = testType;
        AutomaticSuite = automatic;
        Proxy = this.DuckImplement(suiteType);
    }

    /// <summary>
    /// Gets the reverse-duck-type of this object for manual instrumentation
    /// </summary>
    internal object Proxy { get; }

    public TestSuite AutomaticSuite { get; }

    [DuckReverseMethod]
    public string Name => AutomaticSuite.Name;

    [DuckReverseMethod]
    public DateTimeOffset StartTime => AutomaticSuite.StartTime;

    [DuckReverseMethod]
    public object Module => _module.Proxy;

    [DuckReverseMethod]
    public void SetTag(string key, string? value) => AutomaticSuite.SetTag(key, value);

    [DuckReverseMethod]
    public void SetTag(string key, double? value) => AutomaticSuite.SetTag(key, value);

    [DuckReverseMethod]
    public void SetErrorInfo(string type, string message, string? callStack) => AutomaticSuite.SetErrorInfo(type, message, callStack);

    [DuckReverseMethod]
    public void SetErrorInfo(Exception exception) => AutomaticSuite.SetErrorInfo(exception);

    [DuckReverseMethod]
    public void Close() => AutomaticSuite.Close();

    [DuckReverseMethod]
    public void Close(TimeSpan? duration) => AutomaticSuite.Close(duration);

    [DuckReverseMethod]
    public object CreateTest(string name)
    {
        // Using the public APIs here because we _want_ to record them in the telemetry etc
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally
        var test = AutomaticSuite.CreateTest(name);
#pragma warning restore DD0002
        return new ManualTest(this, test, _testType).Proxy;
    }

    [DuckReverseMethod]
    public object CreateTest(string name, DateTimeOffset startDate)
    {
        // Using the public APIs here because we _want_ to record them in the telemetry etc
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally
        var test = AutomaticSuite.CreateTest(name, startDate);
#pragma warning restore DD0002
        return new ManualTest(this, test, _testType).Proxy;
    }
}
