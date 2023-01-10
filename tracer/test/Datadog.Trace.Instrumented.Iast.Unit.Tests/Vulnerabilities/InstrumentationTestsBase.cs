// <copyright file="InstrumentationTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities;

public class InstrumentationTestsBase
{
    public InstrumentationTestsBase()
    {
        AssertInstrumented();
        var traceContext = (Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext;
        traceContext.EnableIastInRequest();
    }

    protected void AddTainted(string tainted)
    {
        var traceContext = (Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext;
        traceContext.IastRequestContext.AddTaintedForTest(null, tainted);
    }

    protected void AssertTainted(string tainted)
    {
        var traceContext = (Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext;
        traceContext.IastRequestContext.GetTainted(tainted).Should().NotBeNull();
    }

    protected void AssertNotTainted(string value)
    {
        var traceContext = (Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext;
        traceContext.IastRequestContext.GetTainted(value).Should().BeNull();
    }

    protected void AssertInstrumented()
    {
        var message = "Test is not instrumented." + EnvironmentVariableMessage("CORECLR_ENABLE_PROFILING") +
            EnvironmentVariableMessage("CORECLR_PROFILER_PATH_64") + EnvironmentVariableMessage("CORECLR_PROFILER_PATH_32") +
            EnvironmentVariableMessage("COR_ENABLE_PROFILING") + EnvironmentVariableMessage("COR_PROFILER_PATH_32") +
            EnvironmentVariableMessage("COR_PROFILER_PATH_64");

        Tracer.Instance.ActiveScope.Should().NotBeNull(message);
    }

    protected void AssertSpanGenerated(string operationName, int spansGenerated = 1)
    {
        var spans = GetGeneratedSpans((Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext);
        spans = spans.Where(x => x.OperationName == operationName).ToList();
        spansGenerated.Should().Be(spans.Count);
    }

    protected void AssertVulnerable(int vulnerabilities = 1)
    {
        var spans = GetGeneratedSpans((Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext);
        vulnerabilities.Should().Be(GetIastSpansCount(spans));
    }

    protected void AssertNotVulnerable()
    {
        AssertVulnerable(0);
    }

    private static string EnvironmentVariableMessage(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return variable + ": " +  (string.IsNullOrEmpty(value) ? "Empty" : value) + Environment.NewLine;
    }

    private int GetIastSpansCount(List<Span> spans)
    {
        return spans.Where(x => x.GetTag(Tags.IastEnabled) != null).Count();
    }

    private List<Span> GetGeneratedSpans(TraceContext context)
    {
        var spans = new List<Span>();
        var contextSpans = context.Spans.GetArray();

        foreach (var span in contextSpans)
        {
            spans.Add(span);
        }

        return spans;
    }
}
