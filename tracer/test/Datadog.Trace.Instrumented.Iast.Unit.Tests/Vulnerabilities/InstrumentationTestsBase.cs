// <copyright file="InstrumentationTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities;

public class InstrumentationTestsBase
{
    protected void AssertVulnerable(int vulnerabilities = 1)
    {
        var spans = GetGeneratedSpans((Tracer.Instance.ActiveScope.Span as Span).Context.TraceContext);
        Assert.Equal(vulnerabilities, GetIastSpansCount(spans));
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
