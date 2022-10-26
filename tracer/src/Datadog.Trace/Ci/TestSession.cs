// <copyright file="TestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test session
/// </summary>
public sealed class TestSession
{
    private static readonly AsyncLocal<TestSession?> CurrentSession = new();
    private readonly Span _span;
    private int _finished;

    private TestSession(string? command, string? workingDirectory, string? framework, DateTimeOffset? startDate)
    {
        // First we make sure that CI Visibility is initialized.
        CIVisibility.Initialize();
        var environment = CIEnvironmentValues.Instance;

        Command = command;
        WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
        Framework = framework;

        WorkingDirectory = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(WorkingDirectory, false);

        var tags = new TestSessionSpanTags
        {
            Command = command ?? string.Empty,
            WorkingDirectory = WorkingDirectory,
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
        };

        if (environment.VariablesToBypass is { } variablesToBypass)
        {
            tags.CiEnvVars = Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(variablesToBypass);
        }

        var span = Tracer.Instance.StartSpan(
            string.IsNullOrEmpty(framework) ? "test_session" : $"{framework!.ToLowerInvariant()}.test_session",
            tags: tags,
            startTime: startDate);

        span.Type = SpanTypes.TestSession;
        span.ResourceName = $"{span.OperationName}.{command}";
        span.Context.TraceContext.SetSamplingPriority((int)SamplingPriority.AutoKeep, SamplingMechanism.Manual);
        span.SetTag(Trace.Tags.Origin, TestTags.CIAppTestOriginName);

        tags.SessionId = span.SpanId;

        _span = span;

        // Inject context to environment variables
        var environmentVariables = new Dictionary<string, string>
        {
            [TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable] = tags.Command,
            [TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable] = tags.WorkingDirectory,
        };

        SpanContextPropagator.Instance.Inject(
            span.Context,
            (IDictionary)environmentVariables,
            new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

        foreach (var envVar in environmentVariables)
        {
            Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
        }

        Current = this;
        CIVisibility.Log.Debug("### Test Session Created: {command}", command);

        if (startDate is null)
        {
            // If a module doesn't have a fixed start time we reset it before running code
            span.ResetStartTime();
        }
    }

    /// <summary>
    /// Gets the session command
    /// </summary>
    public string? Command { get; }

    /// <summary>
    /// Gets the session command working directory
    /// </summary>
    public string? WorkingDirectory { get; }

    /// <summary>
    /// Gets the test session start date
    /// </summary>
    public DateTimeOffset StartTime => _span.StartTime;

    /// <summary>
    /// Gets the test framework
    /// </summary>
    public string? Framework { get; }

    /// <summary>
    /// Gets or sets the current TestSession
    /// </summary>
    internal static TestSession? Current
    {
        get => CurrentSession.Value;
        set => CurrentSession.Value = value;
    }

    internal TestSessionSpanTags Tags => (TestSessionSpanTags)_span.Tags;

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <returns>New test session instance</returns>
    public static TestSession GetOrCreate(string command)
    {
        if (Current is { } current)
        {
            return current;
        }

        return new TestSession(command, null, null, null);
    }

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <returns>New test session instance</returns>
    public static TestSession GetOrCreate(string command, string workingDirectory)
    {
        if (Current is { } current)
        {
            return current;
        }

        return new TestSession(command, workingDirectory, null, null);
    }

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <returns>New test session instance</returns>
    public static TestSession GetOrCreate(string command, string workingDirectory, string framework)
    {
        if (Current is { } current)
        {
            return current;
        }

        return new TestSession(command, workingDirectory, framework, null);
    }

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test session instance</returns>
    public static TestSession GetOrCreate(string command, string workingDirectory, string framework, DateTimeOffset startDate)
    {
        if (Current is { } current)
        {
            return current;
        }

        return new TestSession(command, workingDirectory, framework, startDate);
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
    /// <param name="status">Test session status</param>
    public void Close(TestStatus status)
    {
        Close(status, null);
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="status">Test session status</param>
    /// <param name="duration">Duration of the test module</param>
    public void Close(TestStatus status, TimeSpan? duration)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return;
        }

        var span = _span;

        // Calculate duration beforehand
        duration ??= span.Context.TraceContext.ElapsedSince(span.StartTime);

        // Set status
        switch (status)
        {
            case TestStatus.Pass:
                Tags.Status = TestTags.StatusPass;
                break;
            case TestStatus.Fail:
                Tags.Status = TestTags.StatusFail;
                break;
            case TestStatus.Skip:
                Tags.Status = TestTags.StatusSkip;
                break;
        }

        span.Finish(duration.Value);

        Current = null;
        CIVisibility.Log.Debug("### Test Session Closed: {command}", Command);
        CIVisibility.FlushSpans();
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <returns>New test module instance</returns>
    public TestModule CreateModule(string name)
    {
        return new TestModule(name, null, null, null, Tags);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <returns>New test module instance</returns>
    public TestModule CreateModule(string name, string framework, string frameworkVersion)
    {
        return new TestModule(name, framework, frameworkVersion, null, Tags);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test module instance</returns>
    public TestModule CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
    {
        return new TestModule(name, framework, frameworkVersion, startDate, Tags);
    }
}
