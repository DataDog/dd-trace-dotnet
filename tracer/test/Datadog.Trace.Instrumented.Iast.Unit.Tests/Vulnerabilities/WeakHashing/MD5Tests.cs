// <copyright file="MD5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Settings;
using FluentAssertions.Equivalency.Tracing;
using Moq;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities.WeakHashing;

public class MD5Tests
{
    [Fact]
    public void GivenAMD5_WhenComputeHash_VulnerabilityIsLogged()
    {
        var rrr = Environment.GetEnvironmentVariable("RRR");
        MD5.Create().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable();
    }

    private void AssertVulnerable(int vulnerabilities = 1)
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
