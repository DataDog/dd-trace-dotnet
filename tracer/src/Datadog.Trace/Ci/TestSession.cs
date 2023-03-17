// <copyright file="TestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test session
/// </summary>
public sealed class TestSession
{
    private static readonly AsyncLocal<TestSession?> CurrentSession = new();
    private readonly Span _span;
    private readonly Dictionary<string, string>? _environmentVariablesToRestore = null;
    private int _finished;

    private TestSession(string? command, string? workingDirectory, string? framework, DateTimeOffset? startDate, bool propagateEnvironmentVariables)
    {
        // First we make sure that CI Visibility is initialized.
        CIVisibility.InitializeFromManualInstrumentation();

        var environment = CIEnvironmentValues.Instance;

        Command = command;
        WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
        Framework = framework;

        WorkingDirectory = environment.MakeRelativePathFromSourceRoot(WorkingDirectory, false);

        var tags = new TestSessionSpanTags
        {
            Command = command ?? string.Empty,
            WorkingDirectory = WorkingDirectory,
        };

        tags.SetCIEnvironmentValues(environment);

        var span = Tracer.Instance.StartSpan(
            string.IsNullOrEmpty(framework) ? "test_session" : $"{framework!.ToLowerInvariant()}.test_session",
            tags: tags,
            startTime: startDate);

        span.Type = SpanTypes.TestSession;
        span.ResourceName = $"{span.OperationName}.{command}";
        span.Context.TraceContext.SetSamplingPriority((int)SamplingPriority.AutoKeep);
        span.Context.TraceContext.Origin = TestTags.CIAppTestOriginName;

        tags.SessionId = span.SpanId;

        _span = span;

        // Inject context to environment variables
        if (propagateEnvironmentVariables)
        {
            _environmentVariablesToRestore = new();
            var environmentVariables = GetPropagateEnvironmentVariables();
            foreach (var envVar in environmentVariables)
            {
                _environmentVariablesToRestore[envVar.Key] = EnvironmentHelpers.GetEnvironmentVariable(envVar.Key);
                EnvironmentHelpers.SetEnvironmentVariable(envVar.Key, envVar.Value);
            }
        }

        Current = this;
        CIVisibility.Log.Debug("### Test Session Created: {Command}", command);

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

        return new TestSession(command, null, null, null, false);
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

        return new TestSession(command, workingDirectory, null, null, false);
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

        return new TestSession(command, workingDirectory, framework, null, false);
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

        return new TestSession(command, workingDirectory, framework, startDate, false);
    }

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="startDate">Test session start date</param>
    /// <param name="propagateEnvironmentVariables">Propagate session data through environment variables (out of proc session)</param>
    /// <returns>New test session instance</returns>
    public static TestSession GetOrCreate(string command, string? workingDirectory, string? framework, DateTimeOffset? startDate, bool propagateEnvironmentVariables)
    {
        if (Current is { } current)
        {
            return current;
        }

        return new TestSession(command, workingDirectory, framework, startDate, propagateEnvironmentVariables);
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
        if (InternalClose(status, duration))
        {
            CIVisibility.Log.Debug("### Test Session Flushing after close: {Command}", Command);
            CIVisibility.Flush();
        }
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="status">Test session status</param>
    /// <returns>Task instance</returns>
    public Task CloseAsync(TestStatus status)
    {
        return CloseAsync(status, null);
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="status">Test session status</param>
    /// <param name="duration">Duration of the test module</param>
    /// <returns>Task instance</returns>
    public Task CloseAsync(TestStatus status, TimeSpan? duration)
    {
        if (InternalClose(status, duration))
        {
            CIVisibility.Log.Debug("### Test Session Flushing after close: {Command}", Command);
            return CIVisibility.FlushAsync();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="status">Test session status</param>
    /// <param name="duration">Duration of the test module</param>
    internal bool InternalClose(TestStatus status, TimeSpan? duration)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return false;
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

        if (_environmentVariablesToRestore is { } envVars)
        {
            foreach (var eVar in envVars)
            {
                EnvironmentHelpers.SetEnvironmentVariable(eVar.Key, eVar.Value);
            }
        }

        Current = null;
        CIVisibility.Log.Debug("### Test Session Closed: {Command} | {Status}", Command, Tags.Status);
        return true;
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

    private Dictionary<string, string> GetPropagateEnvironmentVariables()
    {
        var span = _span;
        var tags = Tags;

        var environmentVariables = new Dictionary<string, string>
        {
            [TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable] = tags.Command,
            [TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable] = tags.WorkingDirectory,
        };

        SpanContextPropagator.Instance.Inject(
            span.Context,
            (IDictionary)environmentVariables,
            new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

        return environmentVariables;
    }
}
