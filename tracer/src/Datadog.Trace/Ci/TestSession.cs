// <copyright file="TestSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Configuration;
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

    private readonly ITestOptimization _testOptimization;
    private readonly Span _span;
    private readonly Dictionary<string, string?>? _environmentVariablesToRestore;
    private readonly string? _propagatedWorkingDirectory;
    private readonly CodeCoverageResultAggregator _codeCoverageResults = new();
    private readonly object _coverageResultIdsLock = new();
    private readonly HashSet<string> _coverageResultIds = new(StringComparer.Ordinal);
    private IpcServer? _ipcServer;
    private int _closing;
    private int _finished;
    private int _ipcMessageCount;

    /// <summary>
    /// Tracks IPC callbacks that passed into the session so close can publish coverage after in-flight messages settle.
    /// </summary>
    private int _activeIpcCallbacks;

    /// <summary>
    /// Counts only coverage IPC messages so the finalization barrier does not unblock on unrelated session-tag messages.
    /// </summary>
    private int _coverageIpcMessageCount;

    private int _coverageIpcReferenceReadFailed;

    private long _lastIpcMessageTicks;

    /// <summary>
    /// Tracks the last coverage IPC callback for the quiet-period drain before the session closes.
    /// </summary>
    private long _lastCoverageIpcMessageTicks;

    private TestSession(string? command, string? workingDirectory, string? framework, DateTimeOffset? startDate, bool propagateEnvironmentVariables)
    {
        // First we make sure that CI Visibility is initialized.
        _testOptimization = TestOptimization.Instance;
        _testOptimization.InitializeFromManualInstrumentation();

        var ciValues = _testOptimization.CIValues;

        Command = command;
        var originalWorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
        _propagatedWorkingDirectory = GetPropagatedWorkingDirectory(originalWorkingDirectory);
        WorkingDirectory = originalWorkingDirectory;
        Framework = framework;

        WorkingDirectory = ciValues.MakeRelativePathFromSourceRoot(WorkingDirectory, false);

        var tags = new TestSessionSpanTags
        {
            Command = command ?? string.Empty,
            WorkingDirectory = WorkingDirectory,
            IntelligentTestRunnerSkippingType = IntelligentTestRunnerTags.SkippingTypeTest,
            IntelligentTestRunnerTestsSkippingEnabled =
                _testOptimization.SkippableFeature is { } sf
                    ? (sf.Enabled ? "true" : "false")
                    : null,
        };

        tags.SetCIEnvironmentValues(ciValues);

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

        // Check if the Test Management feature is enabled and set the flag accordingly
        if (_testOptimization.TestManagementFeature?.Enabled == true)
        {
            span.SetTag(TestTags.TestManagementEnabled, "true");
        }

        // Inject context to environment variables
        if (propagateEnvironmentVariables)
        {
            _environmentVariablesToRestore = new();
            // Actual-skip can be set after session creation, so capture it even when it is not propagated yet.
            _environmentVariablesToRestore[ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip] = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
            // Backfill data can be persisted by child code after session creation, so capture it even when it is not propagated yet.
            _environmentVariablesToRestore[ConfigurationKeys.CIVisibilityItrCoverageBackfillPath] = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
            var environmentVariables = GetPropagateEnvironmentVariables();
            foreach (var envVar in environmentVariables)
            {
                // The propagated environment variable set is assembled dynamically from generated keys and propagation headers, so each previous value must be restored by dictionary key.
#pragma warning disable DD0012
                if (!_environmentVariablesToRestore.ContainsKey(envVar.Key))
                {
                    _environmentVariablesToRestore[envVar.Key] = EnvironmentHelpers.GetEnvironmentVariable(envVar.Key);
                }
#pragma warning restore DD0012
                EnvironmentHelpers.SetEnvironmentVariable(envVar.Key, envVar.Value);
            }
        }

        Current = this;
        lock (OpenedTestSessions)
        {
            OpenedTestSessions.Add(this);
        }

        // Record EventCreate telemetry metric
        if (TelemetryHelper.GetEventTypeWithCodeOwnerAndSupportedCiAndBenchmark(
                MetricTags.CIVisibilityTestingEventType.Session,
                framework == CommonTags.TestingFrameworkNameBenchmarkDotNet) is { } eventTypeWithMetadata)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEventCreated(TelemetryHelper.GetTelemetryTestingFrameworkEnum(framework), eventTypeWithMetadata);
        }

        var sessionTypeTag = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CivisibilityAutoInstrumentationProvider)?.ToBoolean() is true ?
                                 MetricTags.CIVisibilityTestSessionType.AutoInjected :
                                 MetricTags.CIVisibilityTestSessionType.NotAutoInjected;

        TelemetryFactory.Metrics.RecordCountCIVisibilityTestSession(
            _testOptimization.CIValues.MetricTag,
            sessionTypeTag,
            _testOptimization.Settings.Logs ? MetricTags.CIVisibilityTestSessionAgentlessLogSubmission.Enabled : MetricTags.CIVisibilityTestSessionAgentlessLogSubmission.NotEnabled);

        _testOptimization.Log.Debug("### Test Session Created: {Command}", command);

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
    /// Gets a value indicating whether any session-level code coverage result has been recorded.
    /// </summary>
    internal bool HasCodeCoverageResults => _codeCoverageResults.HasResults;

    /// <summary>
    /// Gets a value indicating whether the session has a code coverage result that can publish coverage tags.
    /// </summary>
    internal bool HasPublishableCodeCoverageResult => _codeCoverageResults.HasPublishableResult;

    /// <summary>
    /// Get or create a new Test Session
    /// </summary>
    /// <param name="command">Test session command</param>
    /// <param name="workingDirectory">Test session working directory</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="startDate">Test session start date</param>
    /// <param name="propagateEnvironmentVariables">Propagate session data through environment variables (out of proc session)</param>
    /// <returns>New test session instance</returns>
    internal static TestSession GetOrCreate(string command, string? workingDirectory, string? framework, DateTimeOffset? startDate, bool propagateEnvironmentVariables = false)
    {
        if (Current is { } current)
        {
            return current;
        }

        return new TestSession(command, workingDirectory, framework, startDate, propagateEnvironmentVariables);
    }

    /// <summary>
    /// Gets whether a session-level code coverage result has been recorded for the supplied source.
    /// </summary>
    /// <param name="source">Coverage source to check.</param>
    /// <returns>True when at least one coverage result has been recorded for the source.</returns>
    internal bool HasCodeCoverageResult(CodeCoverageReportSource source)
    {
        return _codeCoverageResults.HasResult(source);
    }

    /// <summary>
    /// Suppresses a session-level code coverage source that was proven unsafe.
    /// </summary>
    /// <param name="source">Coverage source to suppress.</param>
    internal void SuppressCodeCoverageResult(CodeCoverageReportSource source)
    {
        _testOptimization.Log.Debug<CodeCoverageReportSource>("TestSession.SuppressCodeCoverageResult: Source={Source}", source);
        _codeCoverageResults.Suppress(source);
    }

    /// <summary>
    /// Suppresses session-level coverage results that have not validated backend ITR coverage.
    /// </summary>
    /// <param name="source">Coverage source whose unvalidated results should be suppressed.</param>
    internal void SuppressUnvalidatedCodeCoverageResult(CodeCoverageReportSource source)
    {
        _testOptimization.Log.Debug<CodeCoverageReportSource>("TestSession.SuppressUnvalidatedCodeCoverageResult: Source={Source}", source);
        _codeCoverageResults.SuppressUnvalidated(source);
    }

    /// <summary>
    /// Suppresses session-level backfilled coverage results that have not validated backend ITR coverage.
    /// </summary>
    /// <param name="source">Coverage source whose unvalidated backfilled results should be suppressed.</param>
    internal void SuppressUnvalidatedBackfilledCodeCoverageResult(CodeCoverageReportSource source)
    {
        _testOptimization.Log.Debug<CodeCoverageReportSource>("TestSession.SuppressUnvalidatedBackfilledCodeCoverageResult: Source={Source}", source);
        _codeCoverageResults.SuppressUnvalidatedBackfilled(source);
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
            _testOptimization.Log.Debug("### Test Session Flushing after close: {Command}", Command);
            _testOptimization.Flush();
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
            _testOptimization.Log.Debug("### Test Session Flushing after close: {Command}", Command);
            return _testOptimization.FlushAsync();
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
        if (Interlocked.Exchange(ref _closing, 1) == 1)
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

        if (Tags.TestsSkipped is null && _testOptimization.SkippableFeature?.Enabled == true)
        {
            var hasSkippedTestsByItr = _testOptimization.SkippableFeature.HasSkippedTestsByItr(Tags.SessionId) ||
                                       CoverageBackfillDataStore.HasPersistedActualItrSkipForSessionOrLegacy(_testOptimization, Tags.SessionId);
            Tags.TestsSkipped = hasSkippedTestsByItr ? "true" : "false";
        }

        var coverageIpcExpected = CoverageBackfillCapability.ShouldWaitForCoverageIpc(_testOptimization.Settings);
        var shouldWaitForCoverageIpc = Volatile.Read(ref _coverageIpcMessageCount) == 0 &&
                                       coverageIpcExpected;
        DrainIpcMessages(
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(50),
            waitForFirstMessage: shouldWaitForCoverageIpc);
        DrainIpcMessages(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(25), waitForFirstMessage: false);
        if (_ipcServer is not null)
        {
            _ipcServer.Dispose();
            _ipcServer = null;
        }

        WaitForActiveIpcCallbacks(TimeSpan.FromMilliseconds(100));
        // Coverlet XML fallback can be processed in-process before the final close path.
        // In that case there is no persisted fallback result to wait for.
        var waitForCoverletXmlFallback =
            CoverageBackfillCapability.ShouldWaitForCoverletXmlFallback(_testOptimization.Settings) &&
            !HasCodeCoverageResult(CodeCoverageReportSource.CoverletXmlFallback);
        RecordPersistedCoverageIpcResults(
            coverageIpcExpected,
            waitForCoverletXmlFallback,
            out var persistedCoverageIpcReadFailed);
        var canPublishCoverage = !persistedCoverageIpcReadFailed ||
                                 CanPublishAfterPersistedCoverageIpcReadFailure();
        Interlocked.Exchange(ref _finished, 1);
        if (canPublishCoverage)
        {
            PublishCodeCoverage();
        }
        else
        {
            _testOptimization.Log.Debug("TestSession.PublishCodeCoverage: skipped because persisted coverage IPC results could not be read completely.");
        }

        span.Finish(duration.Value);

        // Record EventFinished telemetry metric
        if (TelemetryHelper.GetEventTypeWithCodeOwnerAndSupportedCiAndBenchmarkAndEarlyFlakeDetection(
                MetricTags.CIVisibilityTestingEventType.Session,
                Framework == CommonTags.TestingFrameworkNameBenchmarkDotNet) is { } eventTypeWithMetadata)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEventFinished(
                TelemetryHelper.GetTelemetryTestingFrameworkEnum(Framework),
                eventTypeWithMetadata,
                MetricTags.CIVisibilityTestingEventTypeRetryReason.None,
                MetricTags.CIVisibilityTestingEventTypeTestManagementQuarantinedOrDisabled.None,
                MetricTags.CIVisibilityTestingEventTypeTestManagementAttemptToFix.None);
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

        _testOptimization.Log.Debug("### Test Session Closed: {Command} | {Status}", Command, Tags.Status);
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
    internal TestModule CreateModule(string name, string framework, string frameworkVersion)
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
    internal TestModule CreateModule(string name, string framework, string frameworkVersion, DateTimeOffset startDate)
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
            _testOptimization.Log.Debug("TestSession.Enabling IPC server: {Name}", name);
            _ipcServer = new IpcServer(name);
            _ipcServer.SetMessageReceivedCallback(OnIpcMessageReceived);
            return true;
        }
        catch (Exception ex)
        {
            _testOptimization.Log.Error(ex, "Error enabling IPC server");
            return false;
        }
    }

    /// <summary>
    /// Records a candidate session coverage result for source arbitration.
    /// </summary>
    /// <param name="source">Coverage source that produced the result.</param>
    /// <param name="percentage">Line coverage percentage reported by the source.</param>
    /// <param name="backfilled">Whether backend ITR coverage was used to compute the result.</param>
    /// <param name="executableLines">Executable-line count, when available.</param>
    /// <param name="coveredLines">Covered-line count, when available.</param>
    /// <param name="diagnostic">Compact diagnostic text, when available.</param>
    /// <param name="resultId">Stable result identity used to deduplicate IPC delivery and persisted fallback copies of the same producer result.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    internal void RecordCodeCoverage(CodeCoverageReportSource source, double percentage, bool backfilled = false, double? executableLines = null, double? coveredLines = null, string? diagnostic = null, string? resultId = null, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null)
    {
        _testOptimization.Log.Debug<CodeCoverageReportSource, double, bool>(
            "TestSession.RecordCodeCoverage: Source={Source}, Percentage={Percentage}, Backfilled={Backfilled}",
            source,
            percentage,
            backfilled);
        if (!TryRecordCoverageResultId(resultId))
        {
            _testOptimization.Log.Debug("TestSession.RecordCodeCoverage: duplicate coverage result ignored.");
            return;
        }

        _codeCoverageResults.Add(source, percentage, backfilled, executableLines, coveredLines, diagnostic, backfillValidated, backfillNotApplicable, backfillValidation);
    }

    /// <summary>
    /// Records a merged coverage result that supersedes earlier partial results from the same source.
    /// </summary>
    /// <param name="source">Coverage source that produced the merged result.</param>
    /// <param name="percentage">Line coverage percentage reported by the source.</param>
    /// <param name="backfilled">Whether backend ITR coverage was used to compute the result.</param>
    /// <param name="executableLines">Executable-line count, when available.</param>
    /// <param name="coveredLines">Covered-line count, when available.</param>
    /// <param name="diagnostic">Compact diagnostic text, when available.</param>
    /// <param name="resultId">Stable identity for the merged result.</param>
    /// <param name="supersededResultIds">Stable identities of partial producer results represented by the merged result.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    internal void RecordMergedCodeCoverage(CodeCoverageReportSource source, double percentage, bool backfilled = false, double? executableLines = null, double? coveredLines = null, string? diagnostic = null, string? resultId = null, IReadOnlyCollection<string>? supersededResultIds = null, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null)
    {
        _testOptimization.Log.Debug<CodeCoverageReportSource, double, bool>(
            "TestSession.RecordMergedCodeCoverage: Source={Source}, Percentage={Percentage}, Backfilled={Backfilled}",
            source,
            percentage,
            backfilled);
        if (!TryRecordCoverageResultIds(resultId, supersededResultIds))
        {
            _testOptimization.Log.Debug("TestSession.RecordMergedCodeCoverage: duplicate coverage result ignored.");
            return;
        }

        _codeCoverageResults.Replace(source, percentage, backfilled, executableLines, coveredLines, diagnostic, backfillValidated, backfillNotApplicable, backfillValidation);
    }

    /// <summary>
    /// Waits briefly for IPC callbacks that may have been written near process finalization.
    /// </summary>
    /// <param name="maxWait">Maximum time to wait.</param>
    /// <param name="quietPeriod">Required period without an IPC callback before returning.</param>
    /// <param name="waitForFirstMessage">Whether to wait for the first new IPC message before applying the quiet-period rule.</param>
    internal void DrainIpcMessages(TimeSpan maxWait, TimeSpan quietPeriod, bool waitForFirstMessage)
    {
        if (_ipcServer is null || maxWait <= TimeSpan.Zero || quietPeriod <= TimeSpan.Zero)
        {
            return;
        }

        var initialMessageCount = waitForFirstMessage ?
                                      Volatile.Read(ref _coverageIpcMessageCount) :
                                      Volatile.Read(ref _ipcMessageCount);
        var deadlineTicks = DateTime.UtcNow.Ticks + maxWait.Ticks;
        while (DateTime.UtcNow.Ticks < deadlineTicks)
        {
            var currentMessageCount = waitForFirstMessage ?
                                          Volatile.Read(ref _coverageIpcMessageCount) :
                                          Volatile.Read(ref _ipcMessageCount);
            var hasNewMessage = currentMessageCount > initialMessageCount;
            if (!waitForFirstMessage || hasNewMessage)
            {
                var lastMessageTicks = waitForFirstMessage ?
                                           Volatile.Read(ref _lastCoverageIpcMessageTicks) :
                                           Volatile.Read(ref _lastIpcMessageTicks);
                if (lastMessageTicks == 0 || DateTime.UtcNow.Ticks - lastMessageTicks >= quietPeriod.Ticks)
                {
                    return;
                }
            }

            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// Publishes the selected session coverage result to the session span.
    /// </summary>
    internal void PublishCodeCoverage()
    {
        if (Volatile.Read(ref _coverageIpcReferenceReadFailed) == 1 &&
            !CanPublishAfterCoverageIpcReferenceReadFailure())
        {
            _testOptimization.Log.Debug("TestSession.PublishCodeCoverage: skipped because a referenced coverage IPC result could not be read.");
            return;
        }

        if (!_codeCoverageResults.TryGetBestResult(out var result))
        {
            return;
        }

        _testOptimization.Log.Information<CodeCoverageReportSource, double, bool>(
            "TestSession.PublishCodeCoverage: Source={Source}, Percentage={Percentage}, Backfilled={Backfilled}",
            result.Source,
            result.Percentage,
            result.Backfilled);
        SetTag(CodeCoverageTags.PercentageOfTotalLines, result.Percentage);
        if (result.Backfilled)
        {
            SetTag(CodeCoverageTags.Backfilled, "true");
        }
        else
        {
            SetTag(CodeCoverageTags.Backfilled, (string?)null);
        }
    }

    internal bool RecordPersistedCoverageIpcResults(bool waitForResultFolder = false)
        => RecordPersistedCoverageIpcResults(waitForResultFolder, waitForCoverletXmlFallback: true, out _);

    internal bool RecordPersistedCoverageIpcResults(bool waitForResultFolder, out bool readFailed)
        => RecordPersistedCoverageIpcResults(waitForResultFolder, waitForCoverletXmlFallback: true, out readFailed);

    internal bool RecordPersistedCoverageIpcResults(bool waitForResultFolder, bool waitForCoverletXmlFallback, out bool readFailed)
    {
        if (!CoverageBackfillDataStore.TryReadCoverageIpcResults(_testOptimization, Tags.SessionId, waitForResultFolder, waitForCoverletXmlFallback, out var results, out readFailed))
        {
            return false;
        }

        var recorded = false;
        foreach (var result in results)
        {
            RecordCodeCoverageResult(result);
            recorded = true;
        }

        return recorded;
    }

    private bool CanPublishAfterPersistedCoverageIpcReadFailure()
    {
        // Persisted IPC can only add coverage-tool results below ExternalXml priority.
        // Keep fail-closed behavior for those tool sources, but do not drop an already selected external XML report.
        return _codeCoverageResults.HasBestPublishableResult(CodeCoverageReportSource.ExternalXml);
    }

    private bool CanPublishAfterCoverageIpcReferenceReadFailure()
    {
        // A missing referenced IPC result means a selected coverage-tool result could not be loaded.
        // Keep fail-closed behavior for tool sources, but do not drop an already selected external XML report.
        return _codeCoverageResults.HasBestPublishableResult(CodeCoverageReportSource.ExternalXml);
    }

    private void OnIpcMessageReceived(object message)
    {
        _testOptimization.Log.Debug("TestSession.OnIpcMessageReceived: {Message}", message);

        Interlocked.Increment(ref _activeIpcCallbacks);
        try
        {
            // If the session is already finished, we ignore the message
            if (Interlocked.CompareExchange(ref _finished, 1, 1) == 1)
            {
                return;
            }

            Interlocked.Increment(ref _ipcMessageCount);
            Interlocked.Exchange(ref _lastIpcMessageTicks, DateTime.UtcNow.Ticks);

            // If the message is a SetSessionTagMessage, we set the tag
            if (message is SetSessionTagMessage tagMessage)
            {
                if (tagMessage.Value is not null)
                {
                    _testOptimization.Log.Information("TestSession.ReceiveMessage (meta): {Name}={Value}", tagMessage.Name, tagMessage.Value);
                    SetTag(tagMessage.Name, tagMessage.Value);
                }
                else if (tagMessage.NumberValue is not null)
                {
                    _testOptimization.Log.Information("TestSession.ReceiveMessage (metric): {Name}={Value}", tagMessage.Name, tagMessage.NumberValue);
                    SetTag(tagMessage.Name, tagMessage.NumberValue);
                }
            }
            else if (message is SessionCodeCoverageReferenceMessage codeCoverageReferenceMessage)
            {
                Interlocked.Increment(ref _coverageIpcMessageCount);
                Interlocked.Exchange(ref _lastCoverageIpcMessageTicks, DateTime.UtcNow.Ticks);
                _testOptimization.Log.Information<CodeCoverageReportSource>(
                    "TestSession.ReceiveMessage (code coverage reference): Source={Source}",
                    codeCoverageReferenceMessage.Source);

                if (CoverageBackfillDataStore.TryReadCoverageIpcResult(
                        _testOptimization,
                        Tags.SessionId,
                        codeCoverageReferenceMessage.Source,
                        codeCoverageReferenceMessage.ResultId,
                        out var codeCoverageResult))
                {
                    RecordCodeCoverageResult(codeCoverageResult);
                }
                else
                {
                    Interlocked.Exchange(ref _coverageIpcReferenceReadFailed, 1);
                    _testOptimization.Log.Warning<CodeCoverageReportSource>(
                        "TestSession.ReceiveMessage: Could not resolve persisted code coverage IPC result. Source={Source}",
                        codeCoverageReferenceMessage.Source);
                    CoverageBackfillDataStore.RecordCoverageIpcFailure(_testOptimization, Tags.SessionId, codeCoverageReferenceMessage.Source.ToString());
                }
            }
            else if (message is SessionCodeCoverageMessage { Value: >= 0.0 } codeCoverageMessage)
            {
                Interlocked.Increment(ref _coverageIpcMessageCount);
                Interlocked.Exchange(ref _lastCoverageIpcMessageTicks, DateTime.UtcNow.Ticks);
                _testOptimization.Log.Information<CodeCoverageReportSource, double, bool>(
                    "TestSession.ReceiveMessage (code coverage): Source={Source}, Value={Value}, Backfilled={Backfilled}",
                    codeCoverageMessage.Source,
                    codeCoverageMessage.Value,
                    codeCoverageMessage.Backfilled);
                RecordCodeCoverageResult(
                    new CodeCoverageAggregationResult(
                        codeCoverageMessage.Source,
                        codeCoverageMessage.Value,
                        codeCoverageMessage.Backfilled,
                        codeCoverageMessage.ExecutableLines,
                        codeCoverageMessage.CoveredLines,
                        codeCoverageMessage.Diagnostic,
                        codeCoverageMessage.ResultId,
                        codeCoverageMessage.BackfillValidated,
                        codeCoverageMessage.BackfillNotApplicable,
                        codeCoverageMessage.BackfillValidation,
                        codeCoverageMessage.SupersededResultIds));
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeIpcCallbacks);
        }
    }

    private void RecordCodeCoverageResult(CodeCoverageAggregationResult result)
    {
        if (result.SupersededResultIds is { Length: > 0 } supersededResultIds)
        {
            RecordMergedCodeCoverage(
                result.Source,
                result.Percentage,
                result.Backfilled,
                result.ExecutableLines,
                result.CoveredLines,
                result.Diagnostic,
                result.ResultId,
                supersededResultIds,
                result.BackfillValidated,
                result.BackfillNotApplicable,
                result.BackfillValidation);
            return;
        }

        RecordCodeCoverage(
            result.Source,
            result.Percentage,
            result.Backfilled,
            result.ExecutableLines,
            result.CoveredLines,
            result.Diagnostic,
            result.ResultId,
            result.BackfillValidated,
            result.BackfillNotApplicable,
            result.BackfillValidation);
    }

    private bool TryRecordCoverageResultId(string? resultId)
        => TryRecordCoverageResultIds(resultId, supersededResultIds: null);

    private bool TryRecordCoverageResultIds(string? resultId, IReadOnlyCollection<string>? supersededResultIds)
    {
        if (StringUtil.IsNullOrEmpty(resultId) &&
            supersededResultIds is not { Count: > 0 })
        {
            return true;
        }

        lock (_coverageResultIdsLock)
        {
            var duplicateResultId = false;
            if (!StringUtil.IsNullOrEmpty(resultId) &&
                !_coverageResultIds.Add(resultId!))
            {
                duplicateResultId = true;
            }

            if (supersededResultIds is not null)
            {
                foreach (var supersededResultId in supersededResultIds)
                {
                    if (!StringUtil.IsNullOrEmpty(supersededResultId))
                    {
                        _coverageResultIds.Add(supersededResultId);
                    }
                }
            }

            return !duplicateResultId;
        }
    }

    /// <summary>
    /// Waits briefly for IPC callbacks already running when the session starts closing.
    /// </summary>
    /// <param name="maxWait">Maximum time to wait for callbacks to finish.</param>
    internal void WaitForActiveIpcCallbacks(TimeSpan maxWait)
    {
        if (maxWait <= TimeSpan.Zero)
        {
            return;
        }

        var deadlineTicks = DateTime.UtcNow.Ticks + maxWait.Ticks;
        while (Volatile.Read(ref _activeIpcCallbacks) > 0 && DateTime.UtcNow.Ticks < deadlineTicks)
        {
            Thread.Sleep(5);
        }
    }

    private string? GetPropagatedWorkingDirectory(string? workingDirectory)
    {
        if (StringUtil.IsNullOrEmpty(workingDirectory))
        {
            return workingDirectory;
        }

        if (IsForeignWindowsRootedPath(workingDirectory!))
        {
            return workingDirectory;
        }

        try
        {
            return Path.GetFullPath(workingDirectory!);
        }
        catch (Exception)
        {
            return workingDirectory;
        }
    }

    private bool IsForeignWindowsRootedPath(string path)
    {
        return Path.DirectorySeparatorChar != '\\' &&
               path.Length >= 3 &&
               path[1] == ':' &&
               ((path[2] == '\\') || (path[2] == '/')) &&
               IsAsciiLetter(path[0]);
    }

    private bool IsAsciiLetter(char value)
    {
        return (value >= 'A' && value <= 'Z') ||
               (value >= 'a' && value <= 'z');
    }

    private Dictionary<string, string?> GetPropagateEnvironmentVariables()
    {
        var span = _span;
        var tags = Tags;

        var environmentVariables = new Dictionary<string, string?>
        {
            [ConfigurationKeys.CIVisibility.TestSessionCommand] = tags.Command,
            [ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory] = _propagatedWorkingDirectory ?? tags.WorkingDirectory,
            [ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder] = CoverageBackfillDataStore.GetOrCreateRunFolder(_testOptimization),
        };

        var currentBackfillDataPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        if (currentBackfillDataPath is { Length: > 0 })
        {
            environmentVariables[ConfigurationKeys.CIVisibilityItrCoverageBackfillPath] = currentBackfillDataPath;
        }

        var currentBackfillCommand = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        if (currentBackfillCommand is { Length: > 0 })
        {
            environmentVariables[ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand] = currentBackfillCommand;
        }

        var skippableFeature = _testOptimization.SkippableFeature;
        var hasActualItrSkip = skippableFeature is null ?
                                   CoverageBackfillDataStore.HasActualItrSkip() :
                                   skippableFeature.HasSkippedTestsByItr(tags.SessionId) ||
                                   CoverageBackfillDataStore.HasPersistedActualItrSkip(_testOptimization, tags.SessionId);
        if (hasActualItrSkip)
        {
            environmentVariables[ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip] = "1";
        }

        Tracer.Instance.TracerManager.SpanContextPropagator.Inject(
            new PropagationContext(span.Context, Baggage.Current),
            (IDictionary)environmentVariables,
            new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

        return environmentVariables;
    }
}
