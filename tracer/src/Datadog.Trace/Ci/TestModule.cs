// <copyright file="TestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test module
/// </summary>
public sealed class TestModule
{
    private static readonly AsyncLocal<TestModule?> CurrentModule = new();
    private readonly long _timestamp;
    private readonly Dictionary<string, TestSuite> _suites;
    private readonly Dictionary<string, string> _tags;
    private Dictionary<string, double>? _metrics;
    private int _finished;

    private TestModule(string? bundle = null, string? framework = null, string? frameworkVersion = null, DateTimeOffset? startDate = null)
    {
        var environment = CIEnvironmentValues.Instance;
        var frameworkDescription = FrameworkDescription.Instance;

        Bundle = bundle;
        Framework = framework;
        FrameworkVersion = frameworkVersion;
        _suites = new Dictionary<string, TestSuite>();
        _tags = new Dictionary<string, string>
        {
            [CommonTags.CIProvider] = environment.Provider,
            [CommonTags.CIPipelineId] = environment.PipelineId,
            [CommonTags.CIPipelineName] = environment.PipelineName,
            [CommonTags.CIPipelineNumber] = environment.PipelineNumber,
            [CommonTags.CIPipelineUrl] = environment.PipelineUrl,
            [CommonTags.CIJobUrl] = environment.JobUrl,
            [CommonTags.CIJobName] = environment.JobName,
            [CommonTags.StageName] = environment.StageName,
            [CommonTags.CIWorkspacePath] = environment.WorkspacePath,
            [CommonTags.GitRepository] = environment.Repository,
            [CommonTags.GitCommit] = environment.Commit,
            [CommonTags.GitBranch] = environment.Branch,
            [CommonTags.GitTag] = environment.Tag,
            [CommonTags.GitCommitAuthorName] = environment.AuthorName,
            [CommonTags.GitCommitAuthorEmail] = environment.AuthorEmail,
            [CommonTags.GitCommitCommitterName] = environment.CommitterName,
            [CommonTags.GitCommitCommitterEmail] = environment.CommitterEmail,
            [CommonTags.GitCommitMessage] = environment.Message,
            [CommonTags.BuildSourceRoot] = environment.SourceRoot,
            [CommonTags.LibraryVersion] = TracerConstants.AssemblyVersion,
            [CommonTags.RuntimeName] = frameworkDescription.Name,
            [CommonTags.RuntimeVersion] = frameworkDescription.ProductVersion,
            [CommonTags.RuntimeArchitecture] = frameworkDescription.ProcessArchitecture,
            [CommonTags.OSArchitecture] = frameworkDescription.OSArchitecture,
            [CommonTags.OSPlatform] = frameworkDescription.OSPlatform,
            [CommonTags.OSVersion] = Environment.OSVersion.VersionString,
        };
        if (environment.AuthorDate is { } aDate)
        {
            _tags.Add(CommonTags.GitCommitAuthorDate, aDate.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        }

        if (environment.CommitterDate is { } cDate)
        {
            _tags.Add(CommonTags.GitCommitCommitterDate, cDate.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        }

        if (environment.VariablesToBypass is { } variablesToBypass)
        {
            _tags.Add(CommonTags.CiEnvVars, Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(variablesToBypass));
        }

        // Check if Intelligent Test Runner has skippable tests
        if (CIVisibility.HasSkippableTests())
        {
            _tags.Add("_dd.ci.itr.tests_skipped", "true");
        }

        _timestamp = Stopwatch.GetTimestamp();
        StartDate = startDate ?? DateTimeOffset.UtcNow;
        Current = this;
        CIVisibility.Log.Information("### Test Module Created: {bundle}", bundle);
    }

    /// <summary>
    /// Gets the current TestModule
    /// </summary>
    public static TestModule? Current
    {
        get => CurrentModule.Value;
        internal set => CurrentModule.Value = value;
    }

    /// <summary>
    /// Gets the test module start date
    /// </summary>
    public DateTimeOffset StartDate { get; }

    /// <summary>
    /// Gets the test module end date
    /// </summary>
    public DateTimeOffset? EndDate { get; private set; }

    /// <summary>
    /// Gets the test bundle
    /// </summary>
    public string? Bundle { get; }

    /// <summary>
    /// Gets the test framework
    /// </summary>
    public string? Framework { get; }

    /// <summary>
    /// Gets the test framework version
    /// </summary>
    public string? FrameworkVersion { get; }

    /// <summary>
    /// Gets the Module Tags
    /// </summary>
    internal Dictionary<string, string>? Tags => _tags;

    /// <summary>
    /// Gets the Module Metrics
    /// </summary>
    internal Dictionary<string, double>? Metrics => _metrics;

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="bundle">Test suite bundle name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test session instance</returns>
    public static TestModule Create(string? bundle = null, string? framework = null, string? frameworkVersion = null, DateTimeOffset? startDate = null)
    {
        return new TestModule(bundle, framework, frameworkVersion, startDate);
    }

    /// <summary>
    /// Sets a string tag into the test module
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, string? value)
    {
        var tags = _tags;
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
    /// Sets a number tag into the test module
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
    /// Close test module
    /// </summary>
    /// <param name="duration">Duration of the test module</param>
    public void Close(TimeSpan? duration = null)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return;
        }

        EndDate = StartDate.Add(duration ?? StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp));

        lock (_suites)
        {
            if (_suites.Count > 0)
            {
                foreach (var suite in _suites.Values.ToArray())
                {
                    suite.Close(duration);
                }
            }
        }

        Current = null;
        CIVisibility.Log.Information("### Test Module Closed: {bundle}", Bundle);
        CIVisibility.FlushSpans();
    }

    /// <summary>
    /// Create a new test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <param name="startDate">Test suite start date</param>
    /// <returns>Test suite instance</returns>
    public TestSuite CreateSuite(string name, DateTimeOffset? startDate = null)
    {
        var suite = TestSuite.Create(this, name, startDate);
        lock (_suites)
        {
            _suites[name] = suite;
        }

        return suite;
    }

    /// <summary>
    /// Gets an existing test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <returns>Test suite instance</returns>
    public TestSuite? GetSuite(string name)
    {
        lock (_suites)
        {
            return _suites.TryGetValue(name, out var suite) ? suite : null;
        }
    }

    internal void RemoveSuite(string name)
    {
        lock (_suites)
        {
            _suites.Remove(name);
        }
    }
}
