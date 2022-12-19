// <copyright file="InstrumentationTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities;

public class InstrumentationTestsBase
{
    public InstrumentationTestsBase()
    {
        AssertInstrumented();
    }

    protected void AssertInstrumented()
    {
        Tracer.Instance.ActiveScope.Should().NotBeNull("Test is not instrumented");
    }

    protected void AssertSpanGenerated(string operationName, int spansGenerated = 1)
    {
        var spans = GetGeneratedSpans((Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext);
        spans = spans.Where(x => x.OperationName == operationName).ToList();
        spansGenerated.Should().Be(spans.Count);
    }

    protected void AssertVulnerable(int vulnerabilities = 1)
    {
#if !NETFRAMEWORK
        var spans = GetGeneratedSpans((Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext);
        vulnerabilities.Should().Be(GetIastSpansCount(spans));
#else
        var i = Tracer.Instance;
        var s = i.ActiveScope.Span as Span;
        var span = Tracer.Instance.ActiveScope.Span.Context;
        var property = span.GetType().GetProperty("TraceContext", BindingFlags.NonPublic | BindingFlags.Instance);
        var context1 = property.GetValue(span);
        var context = ((Span)span).Context;
        var spans = GetGeneratedSpans(context.TraceContext);
        Assert.Equal(vulnerabilities, GetIastSpansCount(spans));
#endif
    }

    protected void AssertNotVulnerable()
    {
        AssertVulnerable(0);
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
