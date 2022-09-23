// <copyright file="TestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test suite
/// </summary>
public sealed class TestSuite
{
    private static readonly AsyncLocal<TestSuite?> CurrentSuite = new();
    private readonly Span _span;
    private int _finished;

    private TestSuite(TestModule module, string name, DateTimeOffset? startDate = null)
    {
        Module = module;
        Name = name;

        if (string.IsNullOrEmpty(module.Framework))
        {
            _span = Tracer.Instance.StartSpan("test_suite", startTime: startDate);
        }
        else
        {
            _span = Tracer.Instance.StartSpan($"{module.Framework!.ToLowerInvariant()}.test_suite", startTime: startDate);
        }

        var span = _span;
        span.Type = SpanTypes.TestSuite;
        span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
        span.ResourceName = name;
        span.SetTag(Trace.Tags.Origin, TestTags.CIAppTestOriginName);
        span.SetTag(TestTags.Type, TestTags.TypeTest);

        // Suite
        span.SetTag(TestTags.Suite, name);
        span.SetTag(TestSuiteVisibilityTags.TestModuleId, Module.ModuleId.ToString());

        // Copy module tags to the span
        module.CopyTagsToSpan(span);

        if (startDate is null)
        {
            // If a test doesn't have a fixed start time we reset it before running the test code
            span.ResetStartTime();
        }

        Current = this;
        CIVisibility.Log.Information("###### New Test Suite Created: {name} ({module})", Name, Module.Name);
    }

    /// <summary>
    /// Gets the current TestSuite
    /// </summary>
    public static TestSuite? Current
    {
        get => CurrentSuite.Value;
        internal set => CurrentSuite.Value = value;
    }

    /// <summary>
    /// Gets the test suite name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the test suite start date
    /// </summary>
    public DateTimeOffset StartDate => _span.StartTime;

    /// <summary>
    /// Gets the test module for this suite
    /// </summary>
    public TestModule Module { get; }

    internal ulong SuiteId => _span.SpanId;

    /// <summary>
    /// Create a new Test Suite
    /// </summary>
    /// <param name="module">Test module instance</param>
    /// <param name="name">Test suite name</param>
    /// <param name="startDate">Test suite start date</param>
    /// <returns>New test suite instance</returns>
    internal static TestSuite Create(TestModule module, string name, DateTimeOffset? startDate = null)
    {
        return new TestSuite(module, name, startDate);
    }

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
    /// Close test suite
    /// </summary>
    /// <param name="duration">Duration of the test suite</param>
    public void Close(TimeSpan? duration = null)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return;
        }

        var span = _span;

        // Calculate duration beforehand
        duration ??= span.Context.TraceContext.ElapsedSince(span.StartTime);

        // Update status
        if (span.GetTag(TestTags.Status) is { } status)
        {
            if (status == TestTags.StatusFail)
            {
                Module.SetTag(TestTags.Status, TestTags.StatusFail);
            }
        }
        else
        {
            span.SetTag(TestTags.Status, TestTags.StatusPass);
        }

        // Finish
        span.Finish(duration.Value);

        Current = null;
        Module.RemoveSuite(Name);
        CIVisibility.Log.Information("###### Test Suite Closed: {name} ({module})", Name, Module.Name);
    }

    /// <summary>
    /// Create a new test for this suite
    /// </summary>
    /// <param name="name">Name of the test</param>
    /// <param name="startDate">Test start date</param>
    /// <returns>Test instance</returns>
    public Test CreateTest(string name, DateTimeOffset? startDate = null)
    {
        return Test.Create(this, name, startDate);
    }

    internal void CopyTagsToSpan(Span span)
    {
        var processor = new CopyProcessor(span);
        var tags = _span.Tags;
        tags.EnumerateTags(ref processor);
        tags.EnumerateMetrics(ref processor);
    }
}
