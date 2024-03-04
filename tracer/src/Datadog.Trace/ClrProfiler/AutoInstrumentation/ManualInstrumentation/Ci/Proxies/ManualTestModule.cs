// <copyright file="ManualTestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

/// <summary>
/// Automatic instrumentation uses reverse duck-typing to create this
/// </summary>
internal class ManualTestModule
{
    private readonly Type _suiteType;
    private readonly Type _testType;

    public ManualTestModule(TestModule automatic, Type moduleType, Type suiteType, Type testType)
    {
        _suiteType = suiteType;
        _testType = testType;
        AutomaticModule = automatic;
        Proxy = this.DuckImplement(moduleType);
    }

    /// <summary>
    /// Gets the reverse-duck-type of this object for manual instrumentation
    /// </summary>
    internal object Proxy { get; }

    public TestModule AutomaticModule { get; }

    [DuckReverseMethod]
    public string Name => AutomaticModule.Name;

    [DuckReverseMethod]
    public DateTimeOffset StartTime => AutomaticModule.StartTime;

    [DuckReverseMethod]
    public string? Framework => AutomaticModule.Framework;

    [DuckReverseMethod]
    public void SetTag(string key, string? value) => AutomaticModule.SetTag(key, value);

    [DuckReverseMethod]
    public void SetTag(string key, double? value) => AutomaticModule.SetTag(key, value);

    [DuckReverseMethod]
    public void SetErrorInfo(string type, string message, string? callStack) => AutomaticModule.SetErrorInfo(type, message, callStack);

    [DuckReverseMethod]
    public void SetErrorInfo(Exception exception) => AutomaticModule.SetErrorInfo(exception);

    [DuckReverseMethod]
    public void Close() => AutomaticModule.Close();

    [DuckReverseMethod]
    public void Close(TimeSpan? duration) => AutomaticModule.Close(duration);

    [DuckReverseMethod]
    public Task CloseAsync() => AutomaticModule.CloseAsync();

    [DuckReverseMethod]
    public Task CloseAsync(TimeSpan? duration) => AutomaticModule.CloseAsync(duration);

    [DuckReverseMethod]
    public object GetOrCreateSuite(string name)
    {
        // Using the public APIs here because we _want_ to record them in the telemetry etc
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally
        var suite = AutomaticModule.GetOrCreateSuite(name);
#pragma warning restore DD0002
        return new ManualTestSuite(this, suite, _suiteType, _testType).Proxy;
    }

    [DuckReverseMethod]
    public object GetOrCreateSuite(string name, DateTimeOffset? startDate)
    {
        // Using the public APIs here because we _want_ to record them etc
#pragma warning disable DD0002 // This API is only for public usage and should not be called internally
        var suite = AutomaticModule.GetOrCreateSuite(name, startDate);
#pragma warning restore DD0002
        return new ManualTestSuite(this, suite, _suiteType, _testType).Proxy;
    }
}
