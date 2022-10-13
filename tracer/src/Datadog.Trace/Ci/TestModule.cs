// <copyright file="TestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test module
/// </summary>
public sealed class TestModule
{
    private static readonly AsyncLocal<TestModule?> CurrentModule = new();
    private readonly Span _span;
    private readonly Dictionary<string, TestSuite> _suites;
    private int _finished;

    private TestModule(string name, string? framework, string? frameworkVersion, DateTimeOffset? startDate)
    {
        // First we make sure that CI Visibility is initialized.
        CIVisibility.Initialize();

        var environment = CIEnvironmentValues.Instance;
        var frameworkDescription = FrameworkDescription.Instance;
        _suites = new Dictionary<string, TestSuite>();

        Name = name;
        Framework = framework;

        var tags = new TestModuleSpanTags
        {
            Type = TestTags.TypeTest,
            Module = name,
            Framework = framework,
            FrameworkVersion = frameworkVersion,
            CIProvider = environment.Provider,
            CIPipelineId = environment.PipelineId,
            CIPipelineName = environment.PipelineName,
            CIPipelineNumber = environment.PipelineNumber,
            CIPipelineUrl = environment.PipelineUrl,
            CIJobName = environment.JobName,
            CIJobUrl = environment.JobUrl,
            StageName = environment.StageName,
            CIWorkspacePath = environment.WorkspacePath,
            GitRepository = environment.Repository,
            GitCommit = environment.Commit,
            GitBranch = environment.Branch,
            GitTag = environment.Tag,
            GitCommitAuthorDate = environment.AuthorDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture),
            GitCommitAuthorName = environment.AuthorName,
            GitCommitAuthorEmail = environment.AuthorEmail,
            GitCommitCommitterDate = environment.CommitterDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture),
            GitCommitCommitterName = environment.CommitterName,
            GitCommitCommitterEmail = environment.CommitterEmail,
            GitCommitMessage = environment.Message,
            BuildSourceRoot = environment.SourceRoot,
            LibraryVersion = TracerConstants.AssemblyVersion,
            RuntimeName = frameworkDescription.Name,
            RuntimeVersion = frameworkDescription.ProductVersion,
            RuntimeArchitecture = frameworkDescription.ProcessArchitecture,
            OSArchitecture = frameworkDescription.OSArchitecture,
            OSPlatform = frameworkDescription.OSPlatform,
            OSVersion = Environment.OSVersion.VersionString,
        };

        if (environment.VariablesToBypass is { } variablesToBypass)
        {
            tags.CiEnvVars = Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(variablesToBypass);
        }

        // Check if Intelligent Test Runner has skippable tests
        if (CIVisibility.HasSkippableTests())
        {
            tags.TestsSkipped = "true";
        }

        var span = Tracer.Instance.StartSpan(
            string.IsNullOrEmpty(framework) ? "test_module" : $"{framework!.ToLowerInvariant()}.test_module",
            tags: tags,
            startTime: startDate);

        span.Type = SpanTypes.TestModule;
        span.ResourceName = name;
        span.Context.TraceContext.SetSamplingPriority((int)SamplingPriority.AutoKeep, SamplingMechanism.Manual);
        span.SetTag(Trace.Tags.Origin, TestTags.CIAppTestOriginName);

        tags.ModuleId = span.SpanId;

        _span = span;
        Current = this;
        CIVisibility.Log.Debug("### Test Module Created: {name}", name);

        if (startDate is null)
        {
            // If a module doesn't have a fixed start time we reset it before running code
            span.ResetStartTime();
        }
    }

    /// <summary>
    /// Gets the module name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the test module start date
    /// </summary>
    public DateTimeOffset StartTime => _span.StartTime;

    /// <summary>
    /// Gets the test framework
    /// </summary>
    public string? Framework { get; }

    /// <summary>
    /// Gets or sets the current TestModule
    /// </summary>
    internal static TestModule? Current
    {
        get => CurrentModule.Value;
        set => CurrentModule.Value = value;
    }

    internal TestModuleSpanTags Tags => (TestModuleSpanTags)_span.Tags;

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <returns>New test session instance</returns>
    public static TestModule Create(string name)
    {
        return new TestModule(name, null, null, null);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <returns>New test session instance</returns>
    public static TestModule Create(string name, string framework, string frameworkVersion)
    {
        return new TestModule(name, framework, frameworkVersion, null);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test session instance</returns>
    public static TestModule Create(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
    {
        return new TestModule(name, framework, frameworkVersion, startDate);
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
    /// Close test module
    /// </summary>
    public void Close()
    {
        Close(null);
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="duration">Duration of the test module</param>
    public void Close(TimeSpan? duration)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return;
        }

        var span = _span;

        // Calculate duration beforehand
        duration ??= span.Context.TraceContext.ElapsedSince(span.StartTime);

        var remainingSuites = Array.Empty<TestSuite>();
        lock (_suites)
        {
            if (_suites.Count > 0)
            {
                remainingSuites = _suites.Values.ToArray();
            }
        }

        foreach (var suite in remainingSuites)
        {
            suite.Close();
        }

        // Update status
        Tags.Status ??= TestTags.StatusPass;

        span.Finish(duration.Value);

        Current = null;
        CIVisibility.Log.Debug("### Test Module Closed: {name}", Name);
        CIVisibility.FlushSpans();
    }

    /// <summary>
    /// Create a new test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <returns>Test suite instance</returns>
    public TestSuite GetOrCreateSuite(string name)
    {
        return GetOrCreateSuite(name, null);
    }

    /// <summary>
    /// Create a new test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <param name="startDate">Test suite start date</param>
    /// <returns>Test suite instance</returns>
    public TestSuite GetOrCreateSuite(string name, DateTimeOffset? startDate)
    {
        lock (_suites)
        {
            if (_suites.TryGetValue(name, out var suite))
            {
                return suite;
            }

            suite = new TestSuite(this, name, startDate);
            _suites[name] = suite;
            return suite;
        }
    }

    /// <summary>
    /// Gets an existing test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <returns>Test suite instance</returns>
    internal TestSuite? GetSuite(string name)
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
