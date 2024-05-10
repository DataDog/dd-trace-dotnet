// <copyright file="Test.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Pdb;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test
/// </summary>
public sealed class Test
{
    private static readonly AsyncLocal<Test?> CurrentTest = new();
    private static readonly HashSet<Test> OpenedTests = new();

    private readonly Scope _scope;
    private int _finished;
    private List<Action<Test>>? _onCloseActions;

    internal Test(TestSuite suite, string name, DateTimeOffset? startDate)
        : this(suite, name, startDate, default, 0)
    {
    }

    internal Test(TestSuite suite, string name, DateTimeOffset? startDate, TraceId traceId, ulong spanId)
    {
        Suite = suite;
        var module = suite.Module;

        var tags = new TestSpanTags(Suite.Tags, name);
        var tracer = Tracer.Instance;
        var span = tracer.StartSpan(
            string.IsNullOrEmpty(module.Framework) ? "test" : $"{module.Framework!.ToLowerInvariant()}.test",
            tags: tags,
            startTime: startDate,
            traceId: traceId,
            spanId: spanId);
        var scope = tracer.TracerManager.ScopeManager.Activate(span, true);

        scope.Span.Type = SpanTypes.Test;
        scope.Span.ResourceName = $"{suite.Name}.{name}";
        scope.Span.Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep, SamplingMechanism.Manual);
        scope.Span.Context.TraceContext.Origin = TestTags.CIAppTestOriginName;
        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.CiAppManual);

        _scope = scope;

        if (CIVisibility.Settings.CodeCoverageEnabled == true)
        {
            Coverage.CoverageReporter.Handler.StartSession(module.Framework);
        }

        CurrentTest.Value = this;
        lock (OpenedTests)
        {
            OpenedTests.Add(this);
        }

        CIVisibility.Log.Debug("######### New Test Created: {Name} ({Suite} | {Module})", Name, Suite.Name, Suite.Module.Name);

        if (startDate is null)
        {
            // If a test doesn't have a fixed start time we reset it before running the test code
            scope.Span.ResetStartTime();
        }

        // Record EventCreate telemetry metric
        if (TelemetryHelper.GetEventTypeWithCodeOwnerAndSupportedCiAndBenchmark(
                MetricTags.CIVisibilityTestingEventType.Test,
                module.Framework == CommonTags.TestingFrameworkNameBenchmarkDotNet) is { } eventTypeWithMetadata)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEventCreated(TelemetryHelper.GetTelemetryTestingFrameworkEnum(module.Framework), eventTypeWithMetadata);
        }
    }

    internal bool IsClosed => Interlocked.CompareExchange(ref _finished, 0, 0) == 1;

    /// <summary>
    /// Gets the test name
    /// </summary>
    public string? Name => ((TestSpanTags)_scope.Span.Tags).Name;

    /// <summary>
    /// Gets the test start date
    /// </summary>
    public DateTimeOffset StartTime => _scope.Span.StartTime;

    /// <summary>
    /// Gets the test suite for this test
    /// </summary>
    public TestSuite Suite { get; }

    /// <summary>
    /// Gets or sets the current Test
    /// </summary>
    internal static Test? Current
    {
        get => CurrentTest.Value;
        set => CurrentTest.Value = value;
    }

    /// <summary>
    /// Gets the active tests
    /// </summary>
    internal static IReadOnlyCollection<Test> ActiveTests
    {
        get
        {
            lock (OpenedTests)
            {
                return OpenedTests.Count == 0 ? [] : OpenedTests.ToArray();
            }
        }
    }

    /// <summary>
    /// Sets a string tag into the test
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, string? value)
    {
        _scope.Span.SetTag(key, value);
    }

    /// <summary>
    /// Sets a number tag into the test
    /// </summary>
    /// <param name="key">Key of the tag</param>
    /// <param name="value">Value of the tag</param>
    public void SetTag(string key, double? value)
    {
        _scope.Span.SetMetric(key, value);
    }

    /// <summary>
    /// Set Error Info
    /// </summary>
    /// <param name="type">Error type</param>
    /// <param name="message">Error message</param>
    /// <param name="callStack">Error callstack</param>
    public void SetErrorInfo(string type, string message, string? callStack)
    {
        var span = _scope.Span;
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
        _scope.Span.SetException(exception);
    }

    /// <summary>
    /// Set Test method info
    /// </summary>
    /// <param name="methodInfo">Test MethodInfo instance</param>
    public void SetTestMethodInfo(MethodInfo methodInfo)
    {
        if (MethodSymbolResolver.Instance.TryGetMethodSymbol(methodInfo, out var methodSymbol))
        {
            var startLine = methodSymbol.StartLine;
            // startline refers to the first instruction of method body.
            // In order to improve the source code in the CI Visibility UI, we decrease
            // this value by 2 so <bold>most of the time</bold> will show the missing
            // `{` char and the method signature.
            // There's no an easy way to extract the correct startline number.
            if (startLine > 1)
            {
                startLine -= 2;
            }

            var tags = (TestSpanTags)_scope.Span.Tags;
            tags.SourceFile = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(methodSymbol.File, false);
            tags.SourceStart = startLine;
            tags.SourceEnd = methodSymbol.EndLine;

            if (CIEnvironmentValues.Instance.CodeOwners is { } codeOwners)
            {
                var match = codeOwners.Match("/" + CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(methodSymbol.File, false));
                if (match is not null)
                {
                    tags.CodeOwners = match.Value.GetOwnersString();
                }
            }
        }
    }

    /// <summary>
    /// Set Test traits
    /// </summary>
    /// <param name="traits">Traits dictionary</param>
    public void SetTraits(Dictionary<string, List<string>> traits)
    {
        if (traits?.Count > 0)
        {
            var tags = (TestSpanTags)_scope.Span.Tags;
            tags.Traits = Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(traits);
        }
    }

    /// <summary>
    /// Set Test parameters
    /// </summary>
    /// <param name="parameters">TestParameters instance</param>
    public void SetParameters(TestParameters parameters)
    {
        if (parameters is not null)
        {
            var tags = (TestSpanTags)_scope.Span.Tags;
            tags.Parameters = parameters.ToJSON();
        }
    }

    /// <summary>
    /// Set benchmark metadata
    /// </summary>
    /// <param name="hostInfo">Host info</param>
    /// <param name="jobInfo">Job info</param>
    public void SetBenchmarkMetadata(in BenchmarkHostInfo hostInfo, in BenchmarkJobInfo jobInfo)
    {
        ((TestSpanTags)_scope.Span.Tags).Type = TestTags.TypeBenchmark;

        // Host info
        SetTagIfNotNull(BenchmarkTestTags.HostProcessorName, hostInfo.ProcessorName);
        SetDoubleTagIfNotNull(BenchmarkTestTags.HostProcessorPhysicalProcessorCount, hostInfo.ProcessorCount);
        SetDoubleTagIfNotNull(BenchmarkTestTags.HostProcessorPhysicalCoreCount, hostInfo.PhysicalCoreCount);
        SetDoubleTagIfNotNull(BenchmarkTestTags.HostProcessorLogicalCoreCount, hostInfo.LogicalCoreCount);
        SetDoubleTagIfNotNull(BenchmarkTestTags.HostProcessorMaxFrequencyHertz, hostInfo.ProcessorMaxFrequencyHertz);
        SetTagIfNotNull(BenchmarkTestTags.HostOsVersion, hostInfo.OsVersion);
        SetTagIfNotNull(BenchmarkTestTags.HostRuntimeVersion, hostInfo.RuntimeVersion);
        SetDoubleTagIfNotNull(BenchmarkTestTags.HostChronometerFrequencyHertz, hostInfo.ChronometerFrequencyHertz);
        SetDoubleTagIfNotNull(BenchmarkTestTags.HostChronometerResolution, hostInfo.ChronometerResolution);

        // Job info
        SetTagIfNotNull(BenchmarkTestTags.JobDescription, jobInfo.Description);
        SetTagIfNotNull(BenchmarkTestTags.JobPlatform, jobInfo.Platform);
        SetTagIfNotNull(BenchmarkTestTags.JobRuntimeName, jobInfo.RuntimeName);
        SetTagIfNotNull(BenchmarkTestTags.JobRuntimeMoniker, jobInfo.RuntimeMoniker);

        void SetTagIfNotNull(string tag, string? value)
        {
            if (value is not null)
            {
                SetTag(tag, value);
            }
        }

        void SetDoubleTagIfNotNull(string tag, double? value)
        {
            if (value is not null)
            {
                SetTag(tag, value);
            }
        }
    }

    /// <summary>
    /// Add benchmark data
    /// </summary>
    /// <param name="measureType">Measure type</param>
    /// <param name="info">Measure info</param>
    /// <param name="statistics">Statistics values</param>
    public void AddBenchmarkData(BenchmarkMeasureType measureType, string info, in BenchmarkDiscreteStats statistics)
    {
        var measureTypeAsString = measureType switch
        {
            BenchmarkMeasureType.Duration => "duration",
            BenchmarkMeasureType.RunTime => "run_time",
            BenchmarkMeasureType.ApplicationLaunch => "application_launch",
            BenchmarkMeasureType.MeanHeapAllocations => "mean_heap_allocations",
            BenchmarkMeasureType.TotalHeapAllocations => "total_heap_allocations",
            BenchmarkMeasureType.GarbageCollectorGen0 => "gc_gen0_collections",
            BenchmarkMeasureType.GarbageCollectorGen1 => "gc_gen1_collections",
            BenchmarkMeasureType.GarbageCollectorGen2 => "gc_gen2_collections",
            BenchmarkMeasureType.MemoryTotalOperations => "memory_total_operations",
            _ => string.Empty,
        };

        SetTag($"benchmark.{measureTypeAsString}.run", statistics.N);
        SetTag($"benchmark.{measureTypeAsString}.mean", GetValidDoubleValue(statistics.Mean));
        SetTag($"benchmark.{measureTypeAsString}.info", info);
        SetTag($"benchmark.{measureTypeAsString}.statistics.n", statistics.N);
        SetTag($"benchmark.{measureTypeAsString}.statistics.max", GetValidDoubleValue(statistics.Max));
        SetTag($"benchmark.{measureTypeAsString}.statistics.min", GetValidDoubleValue(statistics.Min));
        SetTag($"benchmark.{measureTypeAsString}.statistics.mean", GetValidDoubleValue(statistics.Mean));
        SetTag($"benchmark.{measureTypeAsString}.statistics.median", GetValidDoubleValue(statistics.Median));
        SetTag($"benchmark.{measureTypeAsString}.statistics.std_dev", GetValidDoubleValue(statistics.StandardDeviation));
        SetTag($"benchmark.{measureTypeAsString}.statistics.std_err", GetValidDoubleValue(statistics.StandardError));
        SetTag($"benchmark.{measureTypeAsString}.statistics.kurtosis", GetValidDoubleValue(statistics.Kurtosis));
        SetTag($"benchmark.{measureTypeAsString}.statistics.skewness", GetValidDoubleValue(statistics.Skewness));
        SetTag($"benchmark.{measureTypeAsString}.statistics.p90", GetValidDoubleValue(statistics.P90));
        SetTag($"benchmark.{measureTypeAsString}.statistics.p95", GetValidDoubleValue(statistics.P95));
        SetTag($"benchmark.{measureTypeAsString}.statistics.p90", GetValidDoubleValue(statistics.P99));

        static double GetValidDoubleValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0;
            }

            return value;
        }
    }

    /// <summary>
    /// Close test
    /// </summary>
    /// <param name="status">Test status</param>
    public void Close(TestStatus status)
    {
        Close(status, null, null);
    }

    /// <summary>
    /// Close test
    /// </summary>
    /// <param name="status">Test status</param>
    /// <param name="duration">Duration of the test suite</param>
    public void Close(TestStatus status, TimeSpan? duration)
    {
        Close(status, duration, null);
    }

    /// <summary>
    /// Close test
    /// </summary>
    /// <param name="status">Test status</param>
    /// <param name="duration">Duration of the test suite</param>
    /// <param name="skipReason">In case </param>
    public void Close(TestStatus status, TimeSpan? duration, string? skipReason)
    {
        if (Interlocked.Exchange(ref _finished, 1) == 1)
        {
            CIVisibility.Log.Warning("Test.Close() was already called before.");
            return;
        }

        var scope = _scope;
        var tags = (TestSpanTags)scope.Span.Tags;

        // Calculate duration beforehand
        duration ??= _scope.Span.Context.TraceContext.Clock.ElapsedSince(scope.Span.StartTime);

        // Set coverage
        if (CIVisibility.Settings.CodeCoverageEnabled == true)
        {
            if (Coverage.CoverageReporter.Handler.EndSession() is Coverage.Models.Tests.TestCoverage testCoverage)
            {
                testCoverage.SessionId = tags.SessionId;
                testCoverage.SuiteId = tags.SuiteId;
                testCoverage.SpanId = _scope.Span.SpanId;

                CIVisibility.Log.Debug("Coverage data for SessionId={SessionId}, SuiteId={SuiteId} and SpanId={SpanId} processed.", testCoverage.SessionId, testCoverage.SuiteId, testCoverage.SpanId);
                CIVisibility.Manager?.WriteEvent(testCoverage);
            }
            else if (status != TestStatus.Skip)
            {
                var testName = scope.Span.ResourceName;
                CIVisibility.Log.Warning("Coverage data for test: {TestName} with Status: {Status} is empty. File: {File}", testName, status, tags.SourceFile);
            }
        }

        // Set status
        switch (status)
        {
            case TestStatus.Pass:
                tags.Status = TestTags.StatusPass;
                break;
            case TestStatus.Fail:
                tags.Status = TestTags.StatusFail;
                Suite.Tags.Status = TestTags.StatusFail;
                break;
            case TestStatus.Skip:
                tags.Status = TestTags.StatusSkip;
                tags.SkipReason = skipReason;
                if (tags.SkipReason == IntelligentTestRunnerTags.SkippedByReason)
                {
                    tags.SkippedByIntelligentTestRunner = "true";
                    Suite.Tags.AddIntelligentTestRunnerSkippingCount(1);
                    TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkipped(MetricTags.CIVisibilityTestingEventType.Test);
                }
                else
                {
                    tags.SkippedByIntelligentTestRunner = "false";
                }

                break;
        }

        if (tags.Unskippable is not null && string.Equals(tags.Unskippable, "true", StringComparison.OrdinalIgnoreCase))
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityITRUnskippable(MetricTags.CIVisibilityTestingEventType.Test);
        }

        if (tags.ForcedRun is not null && string.Equals(tags.ForcedRun, "true", StringComparison.OrdinalIgnoreCase))
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityITRForcedRun(MetricTags.CIVisibilityTestingEventType.Test);
        }

        // Call close actions
        if (_onCloseActions is not null)
        {
            foreach (var action in _onCloseActions)
            {
                action(this);
            }

            _onCloseActions.Clear();
        }

        // Finish
        scope.Span.Finish(duration.Value);
        scope.Dispose();

        // Record EventFinished telemetry metric
        if (TelemetryHelper.GetEventTypeWithCodeOwnerAndSupportedCiAndBenchmarkAndEarlyFlakeDetection(
                MetricTags.CIVisibilityTestingEventType.Test,
                tags.Type == TestTags.TypeBenchmark,
                tags.EarlyFlakeDetectionTestIsNew == "true",
                tags.EarlyFlakeDetectionTestAbortReason == "slow",
                !string.IsNullOrEmpty(tags.BrowserDriver),
                tags.IsRumActive == "true") is { } eventTypeWithMetadata)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEventFinished(TelemetryHelper.GetTelemetryTestingFrameworkEnum(tags.Framework), eventTypeWithMetadata);
        }

        Current = null;
        lock (OpenedTests)
        {
            OpenedTests.Remove(this);
        }

        CIVisibility.Log.Debug("######### Test Closed: {Name} ({Suite} | {Module}) | {Status}", Name, Suite.Name, Suite.Module.Name, tags.Status);
    }

    internal void ResetStartTime()
    {
        _scope.Span.ResetStartTime();
    }

    internal ISpan GetInternalSpan()
    {
        return _scope.Span;
    }

    internal TestSpanTags GetTags()
    {
        return (TestSpanTags)_scope.Span.Tags;
    }

    internal void SetName(string name)
    {
        ((TestSpanTags)_scope.Span.Tags).Name = name;
    }

    internal void AddOnCloseAction(Action<Test> action)
    {
        _onCloseActions ??= [];
        _onCloseActions.Add(action);
    }
}
