// <copyright file="TestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test session
/// </summary>
public sealed class TestSession
{
    private static readonly AsyncLocal<TestSession?> CurrentSession = new();
    private static readonly HashSet<TestSession> OpenedTestSessions = new();

    private readonly Span _span;
    private readonly Dictionary<string, string?>? _environmentVariablesToRestore = null;
    private IpcServer? _ipcServer = null;
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
            IntelligentTestRunnerSkippingType = IntelligentTestRunnerTags.SkippingTypeTest,
        };

        tags.SetCIEnvironmentValues(environment);

        var span = Tracer.Instance.StartSpan(
            string.IsNullOrEmpty(framework) ? "test_session" : $"{framework!.ToLowerInvariant()}.test_session",
            tags: tags,
            startTime: startDate);
        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.CiAppManual);

        span.Type = SpanTypes.TestSession;
        span.ResourceName = $"{span.OperationName}.{command}";
        span.Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep);
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
        lock (OpenedTestSessions)
        {
            OpenedTestSessions.Add(this);
        }

        CIVisibility.Log.Debug("### Test Session Created: {Command}", command);

        if (startDate is null)
        {
            // If a module doesn't have a fixed start time we reset it before running code
            span.ResetStartTime();
        }

        // Record EventCreate telemetry metric
        if (TelemetryHelper.GetEventTypeWithCodeOwnerAndSupportedCiAndBenchmark(
                MetricTags.CIVisibilityTestingEventType.Session,
                framework == CommonTags.TestingFrameworkNameBenchmarkDotNet) is { } eventTypeWithMetadata)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEventCreated(TelemetryHelper.GetTelemetryTestingFrameworkEnum(framework), eventTypeWithMetadata);
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

    /// <summary>
    /// Gets the active test sessions
    /// </summary>
    internal static IReadOnlyCollection<TestSession> ActiveTestSessions
    {
        get
        {
            lock (OpenedTestSessions)
            {
                return OpenedTestSessions.Count == 0 ? [] : OpenedTestSessions.ToArray();
            }
        }
    }

    internal TestSessionSpanTags Tags => (TestSessionSpanTags)_span.Tags;

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <returns>New test session instance</returns>
    [PublicApi]
    public static TestSession GetOrCreate(string command)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Session);
        return InternalGetOrCreate(command, workingDirectory: null, framework: null, startDate: null);
    }

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <returns>New test session instance</returns>
    [PublicApi]
    public static TestSession GetOrCreate(string command, string workingDirectory)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Session);
        return InternalGetOrCreate(command, workingDirectory, framework: null, startDate: null);
    }

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <returns>New test session instance</returns>
    [PublicApi]
    public static TestSession GetOrCreate(string command, string workingDirectory, string framework)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Session);
        return InternalGetOrCreate(command, workingDirectory, framework, startDate: null);
    }

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test session instance</returns>
    [PublicApi]
    public static TestSession GetOrCreate(string command, string workingDirectory, string framework, DateTimeOffset startDate)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Session);
        return InternalGetOrCreate(command, workingDirectory, framework, startDate);
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
    [PublicApi]
    public static TestSession GetOrCreate(string command, string? workingDirectory, string? framework, DateTimeOffset? startDate, bool propagateEnvironmentVariables)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Session);
        return InternalGetOrCreate(command, workingDirectory, framework, startDate, propagateEnvironmentVariables);
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
    internal static TestSession InternalGetOrCreate(string command, string? workingDirectory, string? framework, DateTimeOffset? startDate, bool propagateEnvironmentVariables = false)
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
        duration ??= span.Context.TraceContext.Clock.ElapsedSince(span.StartTime);

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

        if (_ipcServer is not null)
        {
            _ipcServer.Dispose();
            _ipcServer = null;
        }

        span.Finish(duration.Value);

        // Record EventFinished telemetry metric
        if (TelemetryHelper.GetEventTypeWithCodeOwnerAndSupportedCiAndBenchmarkAndEarlyFlakeDetection(
                MetricTags.CIVisibilityTestingEventType.Session,
                Framework == CommonTags.TestingFrameworkNameBenchmarkDotNet) is { } eventTypeWithMetadata)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEventFinished(TelemetryHelper.GetTelemetryTestingFrameworkEnum(Framework), eventTypeWithMetadata);
        }

        if (_environmentVariablesToRestore is { } envVars)
        {
            foreach (var eVar in envVars)
            {
                EnvironmentHelpers.SetEnvironmentVariable(eVar.Key, eVar.Value);
            }
        }

        Current = null;
        lock (OpenedTestSessions)
        {
            OpenedTestSessions.Remove(this);
        }

        CIVisibility.Log.Debug("### Test Session Closed: {Command} | {Status}", Command, Tags.Status);
        return true;
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <returns>New test module instance</returns>
    [PublicApi]
    public TestModule CreateModule(string name)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Module);
        return InternalCreateModule(name);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <returns>New test module instance</returns>
    internal TestModule InternalCreateModule(string name)
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
    [PublicApi]
    public TestModule CreateModule(string name, string framework, string frameworkVersion)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Module);
        return InternalCreateModule(name, framework, frameworkVersion);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <returns>New test module instance</returns>
    internal TestModule InternalCreateModule(string name, string framework, string frameworkVersion)
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
    [PublicApi]
    public TestModule CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Module);
        return InternalCreateModule(name, framework, frameworkVersion, startDate);
    }

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test module instance</returns>
    internal TestModule InternalCreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
    {
        return new TestModule(name, framework, frameworkVersion, startDate, Tags);
    }

    internal bool EnableIpcServer()
    {
        if (Tags.SessionId == 0)
        {
            return false;
        }

        try
        {
            var name = $"session_{Tags.SessionId}";
            CIVisibility.Log.Debug("TestSession.Enabling IPC server: {Name}", name);
            _ipcServer = new IpcServer(name);
            _ipcServer.SetMessageReceivedCallback(OnIpcMessageReceived);
            return true;
        }
        catch (Exception ex)
        {
            CIVisibility.Log.Error(ex, "Error enabling IPC server");
            return false;
        }
    }

    private void OnIpcMessageReceived(object message)
    {
        CIVisibility.Log.Debug("TestSession.OnIpcMessageReceived: {Message}", message);

        // If the session is already finished, we ignore the message
        if (Interlocked.CompareExchange(ref _finished, 1, 1) == 1)
        {
            return;
        }

        // If the message is a SetSessionTagMessage, we set the tag
        if (message is SetSessionTagMessage tagMessage)
        {
            if (tagMessage.Value is not null)
            {
                CIVisibility.Log.Information("TestSession.ReceiveMessage (meta): {Name}={Value}", tagMessage.Name, tagMessage.Value);
                SetTag(tagMessage.Name, tagMessage.Value);
            }
            else if (tagMessage.NumberValue is not null)
            {
                CIVisibility.Log.Information("TestSession.ReceiveMessage (metric): {Name}={Value}", tagMessage.Name, tagMessage.NumberValue);
                SetTag(tagMessage.Name, tagMessage.NumberValue);
            }
        }
        else if (message is SessionCodeCoverageMessage { Value: >= 0.0 } codeCoverageMessage)
        {
            CIVisibility.Log.Information("TestSession.ReceiveMessage (code coverage): {Value}", codeCoverageMessage.Value);

            // Adds the global code coverage percentage to the session
            SetTag(CodeCoverageTags.Enabled, "true");
            SetTag(CodeCoverageTags.PercentageOfTotalLines, codeCoverageMessage.Value);
        }
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
