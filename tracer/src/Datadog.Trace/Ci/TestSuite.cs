// <copyright file="TestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test suite
/// </summary>
public sealed class TestSuite
{
    private static readonly AsyncLocal<TestSuite?> CurrentSuite = new();
    private readonly Span _span;
    private int _finished;

    internal TestSuite(TestModule module, string name, DateTimeOffset? startDate)
    {
        Module = module;
        Name = name;

        var tags = new TestSuiteSpanTags(module.Tags, name);
        var span = Tracer.Instance.StartSpan(
            string.IsNullOrEmpty(module.Framework) ? "test_suite" : $"{module.Framework!.ToLowerInvariant()}.test_suite",
            tags: tags,
            startTime: startDate);

        span.Type = SpanTypes.TestSuite;
        span.ResourceName = name;
        span.Context.TraceContext.SetSamplingPriority((int)SamplingPriority.AutoKeep);
        span.Context.TraceContext.Origin = TestTags.CIAppTestOriginName;

        tags.SuiteId = span.SpanId;

        _span = span;
        Current = this;
        CIVisibility.Log.Debug("###### New Test Suite Created: {Name} ({Module})", Name, Module.Name);

        if (startDate is null)
        {
            // If a module doesn't have a fixed start time we reset it before running code
            span.ResetStartTime();
        }
    }

    /// <summary>
    /// Gets the test suite name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the test suite start date
    /// </summary>
    public DateTimeOffset StartTime => _span.StartTime;

    /// <summary>
    /// Gets the test module for this suite
    /// </summary>
    public TestModule Module { get; }

    /// <summary>
    /// Gets or sets the current TestSuite
    /// </summary>
    internal static TestSuite? Current
    {
        get => CurrentSuite.Value;
        set => CurrentSuite.Value = value;
    }

    internal TestSuiteSpanTags Tags => (TestSuiteSpanTags)_span.Tags;

    /// <summary>
    /// Sets a string tag into the test
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, string? value)
    {
        _span.SetTag(key, value);
    }

    /// <summary>
    /// Sets a number tag into the test
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, double? value)
    {
        _span.SetMetric(key, value);
    }

    /// <summary>
    /// Set Error Info
    /// </summary>
    /// <param name="type">Error type</param>
    /// <param name="message">Error message</param>
    /// <param name="callStack">Error callstack</param>
    public void SetErrorInfo(string type, string message, string? callStack)
    {
        var span = _span;
        span.Error = true;
        span.SetTag(Trace.Tags.ErrorType, type);
        span.SetTag(Trace.Tags.ErrorMsg, message);
        if (callStack is not null)
        {
            span.SetTag(Trace.Tags.ErrorStack, callStack);
        }
    }

    /// <summary>
    /// Set Error Info from Exception
    /// </summary>
    /// <param name="exception">Exception instance</param>
    public void SetErrorInfo(Exception exception)
    {
        _span.SetException(exception);
    }

    /// <summary>
    /// Close test suite
    /// </summary>
    public void Close()
    {
        Close(null);
    }

    /// <summary>
    /// Close test suite
    /// </summary>
    /// <param name="duration">Duration of the test suite</param>
    public void Close(TimeSpan? duration)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return;
        }

        var span = _span;

        // Calculate duration beforehand
        duration ??= span.Context.TraceContext.ElapsedSince(span.StartTime);

        // Update status
        if (Tags.Status is { } status)
        {
            if (status == TestTags.StatusFail)
            {
                Module.Tags.Status = TestTags.StatusFail;
            }
        }
        else
        {
            Tags.Status = TestTags.StatusPass;
        }

        span.Finish(duration.Value);

        Current = null;
        Module.RemoveSuite(Name);
        CIVisibility.Log.Debug("###### Test Suite Closed: {Name} ({Module}) | {Status}", Name, Module.Name, Tags.Status);
    }

    /// <summary>
    /// Create a new test for this suite
    /// </summary>
    /// <param name="name">Name of the test</param>
    /// <returns>Test instance</returns>
    public Test CreateTest(string name)
    {
        return new Test(this, name, null);
    }

    /// <summary>
    /// Create a new test for this suite
    /// </summary>
    /// <param name="name">Name of the test</param>
    /// <param name="startDate">Test start date</param>
    /// <returns>Test instance</returns>
    public Test CreateTest(string name, DateTimeOffset startDate)
    {
        return new Test(this, name, startDate);
    }
}
