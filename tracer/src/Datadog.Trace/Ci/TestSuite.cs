// <copyright file="TestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test suite
/// </summary>
public sealed class TestSuite
{
    private static readonly AsyncLocal<TestSuite?> CurrentSuite = new();
    private readonly long _timestamp;
    private Dictionary<string, string>? _tags;
    private Dictionary<string, double>? _metrics;

    private TestSuite(TestModule module, string name, DateTimeOffset? startDate = null)
    {
        Name = name;
        Module = module;
        _timestamp = Stopwatch.GetTimestamp();
        StartDate = startDate ?? DateTimeOffset.UtcNow;
        CIVisibility.Log.Information("###### New Test Suite Created: {name} ({module})", Name, Module.Bundle);
    }

    /// <summary>
    /// Gets the current TestSuite
    /// </summary>
    public static TestSuite? Current => CurrentSuite.Value;

    /// <summary>
    /// Gets the test suite name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the test suite start date
    /// </summary>
    public DateTimeOffset StartDate { get; }

    /// <summary>
    /// Gets the test suite end date
    /// </summary>
    public DateTimeOffset? EndDate { get; private set; }

    /// <summary>
    /// Gets the test module for this suite
    /// </summary>
    public TestModule Module { get; }

    /// <summary>
    /// Gets the Suite Tags
    /// </summary>
    internal Dictionary<string, string>? Tags => _tags;

    /// <summary>
    /// Gets the Suite Metrics
    /// </summary>
    internal Dictionary<string, double>? Metrics => _metrics;

    /// <summary>
    /// Create a new Test Suite
    /// </summary>
    /// <param name="module">Test module instance</param>
    /// <param name="name">Test suite name</param>
    /// <param name="startDate">Test suite start date</param>
    /// <returns>New test suite instance</returns>
    internal static TestSuite Create(TestModule module, string name, DateTimeOffset? startDate = null)
    {
        var testSuite = new TestSuite(module, name, startDate);
        CurrentSuite.Value = testSuite;
        return testSuite;
    }

    /// <summary>
    /// Sets a string tag into the test suite
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, string? value)
    {
        var tags = Volatile.Read(ref _tags);

        if (tags is null)
        {
            var newTags = new Dictionary<string, string>();
            tags = Interlocked.CompareExchange(ref _tags, newTags, null) ?? newTags;
        }

        lock (tags)
        {
            if (value is null)
            {
                tags.Remove(key);
                return;
            }

            tags[key] = value;
        }
    }

    /// <summary>
    /// Sets a number tag into the test suite
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, double? value)
    {
        var metrics = Volatile.Read(ref _metrics);

        if (metrics is null)
        {
            var newMetrics = new Dictionary<string, double>();
            metrics = Interlocked.CompareExchange(ref _metrics, newMetrics, null) ?? newMetrics;
        }

        lock (metrics)
        {
            if (value is null)
            {
                metrics.Remove(key);
                return;
            }

            metrics[key] = value.Value;
        }
    }

    /// <summary>
    /// Close test suite
    /// </summary>
    /// <param name="duration">Duration of the test suite</param>
    public void Close(TimeSpan? duration = null)
    {
        EndDate = StartDate.Add(duration ?? StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp));
        CurrentSuite.Value = null;
        Module.RemoveSuite(Name);
        CIVisibility.Log.Information("###### Test Suite Closed: {name} ({module})", Name, Module.Bundle);
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
}
