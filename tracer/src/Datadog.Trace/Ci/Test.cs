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
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Pdb;

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
        scope.Span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
        scope.Span.SetTag(Trace.Tags.Origin, TestTags.CIAppTestOriginName);

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
    /// Gets the current Test
    /// </summary>
    public static Test? Current
    {
        get => CurrentTest.Value;
        internal set => CurrentTest.Value = value;
    }

    /// <summary>
    /// Gets the test name
    /// </summary>
    public string? Name => Tags.Name;

    /// <summary>
    /// Gets the test start date
    /// </summary>
    public DateTimeOffset StartDate => _scope.Span.StartTime;

    /// <summary>
    /// Gets the test suite for this test
    /// </summary>
    public TestSuite Suite { get; }

    private TestSpanTags Tags => (TestSpanTags)_scope.Span.Tags;

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
            var tags = (TestSpanTags)_scope.Span.Tags;
            tags.SourceFile = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(methodSymbol.File, false);
            tags.SourceStart = methodSymbol.StartLine;
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
        if (CIVisibility.Settings.CodeCoverageEnabled == true && Coverage.CoverageReporter.Handler.EndSession() is Coverage.Models.CoveragePayload coveragePayload)
        {
            if (scope.Span is { } span)
            {
                coveragePayload.TraceId = span.TraceId;
                coveragePayload.SpanId = span.SpanId;
            }

            CIVisibility.Log.Debug("Coverage data for TraceId={traceId} and SpanId={spanId} processed.", coveragePayload.TraceId, coveragePayload.SpanId);
            CIVisibility.Manager?.WriteEvent(coveragePayload);
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
        CIVisibility.Log.Debug("######### Test Closed: {name} ({suite} | {module})", Name, Suite.Name, Suite.Module.Name);
    }

    internal void ResetStartDate()
    {
        _scope.Span.ResetStartTime();
    }

    internal ISpan GetInternalSpan()
    {
        return _scope.Span;
    }
}
