// <copyright file="Test.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Pdb;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci;

/// <summary>
/// CI Visibility test
/// </summary>
public sealed class Test
{
    private static readonly AsyncLocal<Test?> CurrentTest = new();
    private readonly Scope _scope;
    private int _finished;

    internal Test(TestSuite suite, string name, DateTimeOffset? startDate)
    {
        Suite = suite;
        var module = suite.Module;

        var tags = new TestSpanTags(Suite.Tags, name);
        var scope = Tracer.Instance.StartActiveInternal(
            string.IsNullOrEmpty(module.Framework) ? "test" : $"{module.Framework!.ToLowerInvariant()}.test",
            tags: tags,
            startTime: startDate);

        scope.Span.Type = SpanTypes.Test;
        scope.Span.ResourceName = $"{suite.Name}.{name}";
        scope.Span.Context.TraceContext.SetSamplingPriority((int)SamplingPriority.AutoKeep, SamplingMechanism.Manual);
        scope.Span.Context.TraceContext.Origin = TestTags.CIAppTestOriginName;

        _scope = scope;

        if (CIVisibility.Settings.CodeCoverageEnabled == true)
        {
            Coverage.CoverageReporter.Handler.StartSession();
        }

        CurrentTest.Value = this;
        CIVisibility.Log.Debug("######### New Test Created: {name} ({suite} | {module})", Name, Suite.Name, Suite.Module.Name);

        if (startDate is null)
        {
            // If a test doesn't have a fixed start time we reset it before running the test code
            scope.Span.ResetStartTime();
        }
    }

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
    public void SetBenchmarkMetadata(BenchmarkHostInfo hostInfo, BenchmarkJobInfo jobInfo)
    {
        ((TestSpanTags)_scope.Span.Tags).Type = TestTags.TypeBenchmark;

        // Host info

        if (hostInfo.ProcessorName is not null)
        {
            SetTag(BenchmarkTestTags.HostProcessorName, hostInfo.ProcessorName);
        }

        if (hostInfo.ProcessorCount is not null)
        {
            SetTag(BenchmarkTestTags.HostProcessorPhysicalProcessorCount, hostInfo.ProcessorCount);
        }

        if (hostInfo.PhysicalCoreCount is not null)
        {
            SetTag(BenchmarkTestTags.HostProcessorPhysicalCoreCount, hostInfo.PhysicalCoreCount);
        }

        if (hostInfo.LogicalCoreCount is not null)
        {
            SetTag(BenchmarkTestTags.HostProcessorLogicalCoreCount, hostInfo.LogicalCoreCount);
        }

        if (hostInfo.ProcessorMaxFrequencyHertz is not null)
        {
            SetTag(BenchmarkTestTags.HostProcessorMaxFrequencyHertz, hostInfo.ProcessorMaxFrequencyHertz);
        }

        if (hostInfo.OsVersion is not null)
        {
            SetTag(BenchmarkTestTags.HostOsVersion, hostInfo.OsVersion);
        }

        if (hostInfo.RuntimeVersion is not null)
        {
            SetTag(BenchmarkTestTags.HostRuntimeVersion, hostInfo.RuntimeVersion);
        }

        if (hostInfo.ChronometerFrequencyHertz is not null)
        {
            SetTag(BenchmarkTestTags.HostChronometerFrequencyHertz, hostInfo.ChronometerFrequencyHertz);
        }

        if (hostInfo.ChronometerResolution is not null)
        {
            SetTag(BenchmarkTestTags.HostChronometerResolution, hostInfo.ChronometerResolution);
        }

        // Job info

        if (jobInfo.Description is not null)
        {
            SetTag(BenchmarkTestTags.JobDescription, jobInfo.Description);
        }

        if (jobInfo.Platform is not null)
        {
            SetTag(BenchmarkTestTags.JobPlatform, jobInfo.Platform);
        }

        if (jobInfo.RuntimeName is not null)
        {
            SetTag(BenchmarkTestTags.JobRuntimeName, jobInfo.RuntimeName);
        }

        if (jobInfo.RuntimeMoniker is not null)
        {
            SetTag(BenchmarkTestTags.JobRuntimeMoniker, jobInfo.RuntimeMoniker);
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
        duration ??= _scope.Span.Context.TraceContext.ElapsedSince(scope.Span.StartTime);

        // Set coverage
        if (CIVisibility.Settings.CodeCoverageEnabled == true && Coverage.CoverageReporter.Handler.EndSession() is Coverage.Models.Tests.TestCoverage testCoverage)
        {
            testCoverage.SessionId = tags.SessionId;
            testCoverage.SuiteId = tags.SuiteId;
            testCoverage.SpanId = _scope.Span.SpanId;

            CIVisibility.Log.Debug("Coverage data for SessionId={sessionId}, SuiteId={suiteId} and SpanId={spanId} processed.", testCoverage.SessionId, testCoverage.SuiteId, testCoverage.SpanId);
            CIVisibility.Manager?.WriteEvent(testCoverage);
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
                break;
        }

        // Finish
        scope.Span.Finish(duration.Value);
        scope.Dispose();

        Current = null;
        CIVisibility.Log.Debug("######### Test Closed: {name} ({suite} | {module}) | {status}", Name, Suite.Name, Suite.Module.Name, tags.Status);
    }

    internal void ResetStartTime()
    {
        _scope.Span.ResetStartTime();
    }

    internal ISpan GetInternalSpan()
    {
        return _scope.Span;
    }
}
