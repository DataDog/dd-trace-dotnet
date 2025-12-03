// <copyright file="TestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test module
/// </summary>
public sealed class TestModule
{
    private static readonly AsyncLocal<TestModule?> CurrentModule = new();
    private static readonly HashSet<TestModule> OpenedTestModules = new();

    private readonly ITestOptimization _testOptimization;
    private readonly Span _span;
    private readonly Dictionary<string, TestSuite> _suites;
    private readonly TestSession? _fakeSession;
    private IpcClient? _ipcClient = null;
    private int _finished;

    private TestModule(string name, string? framework, string? frameworkVersion, DateTimeOffset? startDate)
        : this(name, framework, frameworkVersion, startDate, null)
    {
    }

    internal TestModule(string name, string? framework, string? frameworkVersion, DateTimeOffset? startDate, TestSessionSpanTags? sessionSpanTags)
    {
        // First we make sure that CI Visibility is initialized.
        _testOptimization = TestOptimization.Instance;
        _testOptimization.InitializeFromManualInstrumentation();
        var ciValues = _testOptimization.CIValues;

        var frameworkDescription = FrameworkDescription.Instance;
        _suites = new Dictionary<string, TestSuite>();

        Name = name;
        Framework = framework;

        // if sessionSpanTags is not null then the TestSession is in-proc.
        // otherwise the TestSession is out of process

        TestModuleSpanTags tags;
        if (sessionSpanTags is not null)
        {
            // In-Proc session
            tags = new TestModuleSpanTags
            {
                Type = TestTags.TypeTest,
                Module = name,
                Framework = framework,
                FrameworkVersion = frameworkVersion,
                CIProvider = sessionSpanTags.CIProvider,
                CIPipelineId = sessionSpanTags.CIPipelineId,
                CIPipelineName = sessionSpanTags.CIPipelineName,
                CIPipelineNumber = sessionSpanTags.CIPipelineNumber,
                CIPipelineUrl = sessionSpanTags.CIPipelineUrl,
                CIJobName = sessionSpanTags.CIJobName,
                CIJobUrl = sessionSpanTags.CIJobUrl,
                StageName = sessionSpanTags.StageName,
                CIWorkspacePath = sessionSpanTags.CIWorkspacePath,
                GitRepository = sessionSpanTags.GitRepository,
                GitCommit = sessionSpanTags.GitCommit,
                GitBranch = sessionSpanTags.GitBranch,
                GitTag = sessionSpanTags.GitTag,
                GitCommitAuthorDate = sessionSpanTags.GitCommitAuthorDate,
                GitCommitAuthorName = sessionSpanTags.GitCommitAuthorName,
                GitCommitAuthorEmail = sessionSpanTags.GitCommitAuthorEmail,
                GitCommitCommitterDate = sessionSpanTags.GitCommitCommitterDate,
                GitCommitCommitterName = sessionSpanTags.GitCommitCommitterName,
                GitCommitCommitterEmail = sessionSpanTags.GitCommitCommitterEmail,
                GitCommitMessage = sessionSpanTags.GitCommitMessage,
                BuildSourceRoot = sessionSpanTags.BuildSourceRoot,
                RuntimeName = frameworkDescription.Name,
                RuntimeVersion = frameworkDescription.ProductVersion,
                RuntimeArchitecture = frameworkDescription.ProcessArchitecture,
                OSArchitecture = frameworkDescription.OSArchitecture,
                OSPlatform = frameworkDescription.OSPlatform,
                OSVersion = _testOptimization.HostInfo.GetOperatingSystemVersion(),
                CiEnvVars = sessionSpanTags.CiEnvVars,
                SessionId = sessionSpanTags.SessionId,
                Command = sessionSpanTags.Command,
                WorkingDirectory = sessionSpanTags.WorkingDirectory,
                IntelligentTestRunnerSkippingType = IntelligentTestRunnerTags.SkippingTypeTest,
            };
        }
        else
        {
            // Out-of-Proc session
            tags = new TestModuleSpanTags
            {
                Type = TestTags.TypeTest,
                Module = name,
                Framework = framework,
                FrameworkVersion = frameworkVersion,
                RuntimeName = frameworkDescription.Name,
                RuntimeVersion = frameworkDescription.ProductVersion,
                RuntimeArchitecture = frameworkDescription.ProcessArchitecture,
                OSArchitecture = frameworkDescription.OSArchitecture,
                OSPlatform = frameworkDescription.OSPlatform,
                OSVersion = _testOptimization.HostInfo.GetOperatingSystemVersion(),
                IntelligentTestRunnerSkippingType = IntelligentTestRunnerTags.SkippingTypeTest,
            };

            tags.SetCIEnvironmentValues(ciValues);

            // Extract session variables (from out of process sessions)
            var environmentVariables = EnvironmentHelpers.GetEnvironmentVariables();
            var context = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                environmentVariables,
                new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

            if (context.SpanContext is { } sessionContext)
            {
                tags.SessionId = sessionContext.SpanId;
                if (environmentVariables.TryGetValue<string>(TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable, out var testSessionCommand))
                {
                    tags.Command = testSessionCommand;
                }

                if (environmentVariables.TryGetValue<string>(TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable, out var testSessionWorkingDirectory))
                {
                    tags.WorkingDirectory = testSessionWorkingDirectory;
                }
            }
            else
            {
                _testOptimization.Log.Information("A session cannot be found, creating a fake session as a parent of the module.");
                _fakeSession = TestSession.GetOrCreate(System.Environment.CommandLine, System.Environment.CurrentDirectory, null, startDate, false);
                if (_fakeSession.Tags is { } fakeSessionTags)
                {
                    tags.SessionId = fakeSessionTags.SessionId;
                    tags.Command = fakeSessionTags.Command;
                    tags.WorkingDirectory = fakeSessionTags.WorkingDirectory;

                    if (_testOptimization.EarlyFlakeDetectionFeature?.Enabled == true)
                    {
                        fakeSessionTags.EarlyFlakeDetectionTestEnabled = "true";
                    }
                }

                // Check if the Test Management feature is enabled and set the flag accordingly
                if (_testOptimization.TestManagementFeature?.Enabled == true)
                {
                    _fakeSession.SetTag(TestTags.TestManagementEnabled, "true");
                }
            }
        }

        // Check if Intelligent Test Runner has skippable tests and set the flag according to that
        tags.TestsSkipped = _testOptimization.SkippableFeature?.HasSkippableTests() == true ? "true" : "false";

        // var span = Tracer.Instance.StartSpan(
        //     string.IsNullOrEmpty(framework) ? "test_module" : $"{framework!.ToLowerInvariant()}.test_module",
        //     tags: tags,
        //     startTime: startDate);
        // TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.CiAppManual);
        //
        // span.Type = SpanTypes.TestModule;
        // span.ResourceName = name;
        // span.Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep);
        // span.Context.TraceContext.Origin = TestTags.CIAppTestOriginName;
        //
        // tags.ModuleId = span.SpanId;

        _span = null!;
        Current = this;
        lock (OpenedTestModules)
        {
            OpenedTestModules.Add(this);
        }

        _testOptimization.Log.Debug("### Test Module Created: {Name}", name);

        if (startDate is null)
        {
            // If a module doesn't have a fixed start time we reset it before running code
            // span.ResetStartTime();
        }

        // Record EventCreate telemetry metric
        TelemetryFactory.Metrics.RecordCountCIVisibilityEventCreated(TelemetryHelper.GetTelemetryTestingFrameworkEnum(framework), MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Module);
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

    /// <summary>
    /// Gets the active test modules
    /// </summary>
    internal static IReadOnlyCollection<TestModule> ActiveTestModules
    {
        get
        {
            lock (OpenedTestModules)
            {
                return OpenedTestModules.Count == 0 ? [] : OpenedTestModules.ToArray();
            }
        }
    }

    internal TestModuleSpanTags Tags => (TestModuleSpanTags)_span.Tags;

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <returns>New test module instance</returns>
    internal static TestModule Create(string name, string? framework, string? frameworkVersion)
        => Create(name, framework, frameworkVersion, null);

    /// <summary>
    /// Create a new Test Module
    /// </summary>
    /// <param name="name">Test module name</param>
    /// <param name="framework">Testing framework name</param>
    /// <param name="frameworkVersion">Testing framework version</param>
    /// <param name="startDate">Test session start date</param>
    /// <returns>New test module instance</returns>
    internal static TestModule Create(string name, string? framework, string? frameworkVersion, DateTimeOffset? startDate)
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
    /// <remarks>Use CloseAsync() version whenever possible.</remarks>
    public void Close()
    {
        Close(null);
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <remarks>Use CloseAsync() version whenever possible.</remarks>
    /// <param name="duration">Duration of the test module</param>
    public void Close(TimeSpan? duration)
    {
        if (InternalClose(duration))
        {
            _testOptimization.Log.Debug("### Test Module Flushing after close: {Name}", Name);
            _testOptimization.Flush();
        }
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <returns>Task instance </returns>
    public Task CloseAsync()
    {
        return CloseAsync(null);
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="duration">Duration of the test module</param>
    /// <returns>Task instance </returns>
    public Task CloseAsync(TimeSpan? duration)
    {
        if (InternalClose(duration))
        {
            _testOptimization.Log.Debug("### Test Module Flushing after close: {Name}", Name);
            return _testOptimization.FlushAsync();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Close test module
    /// </summary>
    /// <param name="duration">Duration of the test module</param>
    private bool InternalClose(TimeSpan? duration)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            return false;
        }

        var span = _span;

        // Calculate duration beforehand
        duration ??= span.Context.TraceContext.Clock.ElapsedSince(span.StartTime);

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

        if (_testOptimization.Settings.CodeCoverageEnabled == true &&
            CoverageReporter.Handler is DefaultWithGlobalCoverageEventHandler coverageHandler &&
            coverageHandler.GetCodeCoveragePercentage() is { } globalCoverage)
        {
            // We only report global code coverage if ITR is disabled and we are in a fake session (like the internal testlogger scenario)
            // For a normal customer session we never report the percentage of total lines on modules
            if (!_testOptimization.Settings.IntelligentTestRunnerEnabled && _fakeSession is not null)
            {
                // Adds the global code coverage percentage to the module
                var codeCoveragePercentage = globalCoverage.GetTotalPercentage();
                SetTag(CodeCoverageTags.PercentageOfTotalLines, codeCoveragePercentage);
                _fakeSession.SetTag(CodeCoverageTags.PercentageOfTotalLines, codeCoveragePercentage);
            }

            // If the code coverage path environment variable is set, we store the json file
            if (!string.IsNullOrWhiteSpace(_testOptimization.Settings.CodeCoveragePath))
            {
                var codeCoveragePath = Path.Combine(_testOptimization.Settings.CodeCoveragePath, $"coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}-{Guid.NewGuid():n}.json");
                try
                {
                    using var fStream = File.OpenWrite(codeCoveragePath);
                    using var sWriter = new StreamWriter(fStream, Encoding.UTF8, 4096, false);
                    new JsonSerializer().Serialize(sWriter, globalCoverage);
                }
                catch (Exception ex)
                {
                    _testOptimization.Log.Error(ex, "Error writing global code coverage.");
                }
            }
        }

        if (_testOptimization.SkippableFeature is not null)
        {
            span.SetTag(IntelligentTestRunnerTags.TestTestsSkippingEnabled, _testOptimization.SkippableFeature.Enabled ? "true" : "false");
            if (_testOptimization.SkippableFeature.Enabled)
            {
                // If we detect a module with tests skipping enabled, we ensure we also have the session tag set
                TrySetSessionTag(IntelligentTestRunnerTags.TestTestsSkippingEnabled, "true");
            }
        }

        if (Tags.IntelligentTestRunnerSkippingCount.HasValue)
        {
            span.SetTag(IntelligentTestRunnerTags.TestsSkipped, "true");
            // If we detect a module with tests being skipped, we ensure we also have the session tag set
            // if not we don't affect the session tag (other modules could have skipped tests)
            TrySetSessionTag(IntelligentTestRunnerTags.TestsSkipped, "true");
        }
        else
        {
            span.SetTag(IntelligentTestRunnerTags.TestsSkipped, _testOptimization.SkippableFeature?.HasSkippableTests() == true ? "true" : "false");
            if (_testOptimization.SkippableFeature?.HasSkippableTests() == true)
            {
                // If we detect a module with tests being skipped, we ensure we also have the session tag set
                // if not we don't affect the session tag (other modules could have skipped tests)
                TrySetSessionTag(IntelligentTestRunnerTags.TestsSkipped, "true");
            }
        }

        if (_testOptimization.Settings.CodeCoverageEnabled.HasValue)
        {
            var value = _testOptimization.Settings.CodeCoverageEnabled.Value ? "true" : "false";
            span.SetTag(CodeCoverageTags.Enabled, value);
            if (_testOptimization.Settings.CodeCoverageEnabled.Value)
            {
                // If we confirm that a module has code coverage enabled, we ensure we also have the session tag set
                // if not we leave the tag as is (other modules could have code coverage enabled)
                TrySetSessionTag(CodeCoverageTags.Enabled, "true");
            }
            else
            {
                _fakeSession?.SetTag(CodeCoverageTags.Enabled, value);
            }
        }

        if (_ipcClient is not null)
        {
            _ipcClient.Dispose();
            _ipcClient = null;
        }

        span.Finish(duration.Value);

        // Record EventFinished telemetry metric
        TelemetryFactory.Metrics.RecordCountCIVisibilityEventFinished(
            TelemetryHelper.GetTelemetryTestingFrameworkEnum(Framework),
            MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmarkAndEarlyFlakeDetectionAndRum.Module,
            MetricTags.CIVisibilityTestingEventTypeRetryReason.None,
            MetricTags.CIVisibilityTestingEventTypeTestManagementQuarantinedOrDisabled.None,
            MetricTags.CIVisibilityTestingEventTypeTestManagementAttemptToFix.None);

        Current = null;
        lock (OpenedTestModules)
        {
            OpenedTestModules.Remove(this);
        }

        _testOptimization.Log.Debug("### Test Module Closed: {Name} | {Status}", Name, Tags.Status);

        if (_fakeSession is { } fakeSession)
        {
            switch (Tags.Status)
            {
                case TestTags.StatusPass:
                    fakeSession.InternalClose(TestStatus.Pass, duration.Value);
                    break;
                case TestTags.StatusFail:
                    fakeSession.InternalClose(TestStatus.Fail, duration.Value);
                    break;
                case TestTags.StatusSkip:
                    fakeSession.InternalClose(TestStatus.Skip, duration.Value);
                    break;
                default:
                    fakeSession.InternalClose(TestStatus.Pass, duration.Value);
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Create a new test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <returns>Test suite instance</returns>
    internal TestSuite GetOrCreateSuite(string name)
    {
        return GetOrCreateSuite(name, null);
    }

    /// <summary>
    /// Create a new test suite for this session
    /// </summary>
    /// <param name="name">Name of the test suite</param>
    /// <param name="startDate">Test suite start date</param>
    /// <returns>Test suite instance</returns>
    internal TestSuite GetOrCreateSuite(string name, DateTimeOffset? startDate)
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

    internal bool EnableIpcClient()
    {
        if (_fakeSession != null || Tags.SessionId == 0)
        {
            return false;
        }

        // Span is created in the .ctor so we can use it here for synchronization
        lock (_span)
        {
            if (_ipcClient is not null)
            {
                return true;
            }

            try
            {
                var name = $"session_{Tags.SessionId}";
                _testOptimization.Log.Debug("TestModule.Enabling IPC client: {Name}", name);
                _ipcClient = new IpcClient(name);
                return true;
            }
            catch (Exception ex)
            {
                _testOptimization.Log.Error(ex, "Error enabling IPC client");
                return false;
            }
        }
    }

    internal bool TrySetSessionTag(string name, string value)
    {
        if (_fakeSession is { } fakeSession)
        {
            fakeSession.SetTag(name, value);
            return true;
        }

        if (_ipcClient is { } ipcClient)
        {
            try
            {
                _testOptimization.Log.Debug("TestModule.Sending SetSessionTagMessage: {Name}={Value}", name, value);
                return ipcClient.TrySendMessage(new SetSessionTagMessage(name, value));
            }
            catch (Exception ex)
            {
                _testOptimization.Log.Error(ex, "Error sending SetSessionTagMessage");
            }
        }

        return false;
    }

    internal bool TrySetSessionTag(string name, double value)
    {
        if (_fakeSession is { } fakeSession)
        {
            fakeSession.SetTag(name, value);
            return true;
        }

        if (_ipcClient is { } ipcClient)
        {
            try
            {
                _testOptimization.Log.Debug("TestModule.Sending SetSessionTagMessage: {Name}={Value}", name, value);
                return ipcClient.TrySendMessage(new SetSessionTagMessage(name, value));
            }
            catch (Exception ex)
            {
                _testOptimization.Log.Error(ex, "Error sending SetSessionTagMessage");
            }
        }

        return false;
    }
}
