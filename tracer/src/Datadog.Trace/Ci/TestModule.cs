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
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;

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

    private TestModule(string name, string? framework = null, string? frameworkVersion = null, DateTimeOffset? startDate = null)
    {
        var environment = CIEnvironmentValues.Instance;
        var frameworkDescription = FrameworkDescription.Instance;
        _suites = new Dictionary<string, TestSuite>();

        Name = name;
        Framework = framework;
        FrameworkVersion = frameworkVersion;

        if (string.IsNullOrEmpty(framework))
        {
            _span = Tracer.Instance.StartSpan("test_module", startTime: startDate);
        }
        else
        {
            _span = Tracer.Instance.StartSpan($"{framework!.ToLowerInvariant()}.test_module", startTime: startDate);
        }

        var span = _span;

        span.Type = SpanTypes.TestModule;
        span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
        span.ResourceName = name;
        span.SetTag(Trace.Tags.Origin, TestTags.CIAppTestOriginName);
        span.SetTag(TestTags.Type, TestTags.TypeTest);

        // Module
        span.SetTag(TestTags.Module, name);
        span.SetTag(TestTags.Bundle, name);
        span.SetTag(TestTags.Framework, framework);
        span.SetTag(TestTags.FrameworkVersion, frameworkVersion);

        span.SetTag(CommonTags.CIProvider, environment.Provider);
        span.SetTag(CommonTags.CIPipelineId, environment.PipelineId);
        span.SetTag(CommonTags.CIPipelineName, environment.PipelineName);
        span.SetTag(CommonTags.CIPipelineNumber, environment.PipelineNumber);
        span.SetTag(CommonTags.CIPipelineUrl, environment.PipelineUrl);
        span.SetTag(CommonTags.CIJobUrl, environment.JobUrl);
        span.SetTag(CommonTags.CIJobName, environment.JobName);
        span.SetTag(CommonTags.StageName, environment.StageName);
        span.SetTag(CommonTags.CIWorkspacePath, environment.WorkspacePath);

        span.SetTag(CommonTags.GitRepository, environment.Repository);
        span.SetTag(CommonTags.GitCommit, environment.Commit);
        span.SetTag(CommonTags.GitBranch, environment.Branch);
        span.SetTag(CommonTags.GitTag, environment.Tag);
        span.SetTag(CommonTags.GitCommitAuthorName, environment.AuthorName);
        span.SetTag(CommonTags.GitCommitAuthorEmail, environment.AuthorEmail);
        span.SetTag(CommonTags.GitCommitCommitterName, environment.CommitterName);
        span.SetTag(CommonTags.GitCommitCommitterEmail, environment.CommitterEmail);
        span.SetTag(CommonTags.GitCommitMessage, environment.Message);
        span.SetTag(CommonTags.BuildSourceRoot, environment.SourceRoot);

        span.SetTag(CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);
        span.SetTag(CommonTags.RuntimeName, frameworkDescription.Name);
        span.SetTag(CommonTags.RuntimeVersion, frameworkDescription.ProductVersion);
        span.SetTag(CommonTags.RuntimeArchitecture, frameworkDescription.ProcessArchitecture);
        span.SetTag(CommonTags.OSArchitecture, frameworkDescription.OSArchitecture);
        span.SetTag(CommonTags.OSPlatform, frameworkDescription.OSPlatform);
        span.SetTag(CommonTags.OSVersion, Environment.OSVersion.VersionString);

        if (environment.AuthorDate is { } aDate)
        {
            span.SetTag(CommonTags.GitCommitAuthorDate, aDate.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        }

        if (environment.CommitterDate is { } cDate)
        {
            span.SetTag(CommonTags.GitCommitCommitterDate, cDate.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        }

        if (environment.VariablesToBypass is { } variablesToBypass)
        {
            span.SetTag(CommonTags.CiEnvVars, Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(variablesToBypass));
        }

        // Check if Intelligent Test Runner has skippable tests
        if (CIVisibility.HasSkippableTests())
        {
            span.SetTag("_dd.ci.itr.tests_skipped", "true");
        }

        Current = this;
        CIVisibility.Log.Information("### Test Module Created: {name}", name);
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
    public DateTimeOffset StartDate => _span.StartTime;

    /// <summary>
    /// Gets the module name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the test framework
    /// </summary>
    public string? Framework { get; }

    /// <summary>
    /// Gets the test framework version
    /// </summary>
    public string? FrameworkVersion { get; }

    internal ulong ModuleId => _span.SpanId;

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test session instance</returns>
    public static TestModule Create(string name, string? framework = null, string? frameworkVersion = null, DateTimeOffset? startDate = null)
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
    /// Close test module
    /// </summary>
    /// <param name="duration">Duration of the test module</param>
    public void Close(TimeSpan? duration = null)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return;
        }

        var span = _span;

        // Calculate duration beforehand
        duration ??= span.Context.TraceContext.ElapsedSince(span.StartTime);

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

        // Finish
        span.Finish(duration.Value);

        Current = null;
        CIVisibility.Log.Information("### Test Module Closed: {name}", Name);
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

    internal void CopyTagsToSpan(Span span)
    {
        var processor = new CopyProcessor(span);
        var tags = _span.Tags;
        tags.EnumerateTags(ref processor);
        tags.EnumerateMetrics(ref processor);
    }
}
