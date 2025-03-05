// <copyright file="AppSecContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class AppSecContextTests
{
    [InlineData(-2, -2, -1, -2, -1)]
    [InlineData(2, -2, -1, null, -1)]
    [InlineData(2, 2, 1, null, null)]
    [Theory]
    public void GivenAQuery_WhenWAFError_ThenSpanHasErrorTags(int raspErrorCode, int wafErrorCode, int wafErrorCode2, int? expectedRaspErrorCode, int? expectedWafErrorCode)
    {
        var settings = TracerSettings.Create(new Dictionary<string, object>());
        var tracer = new Tracer(settings, null, null, null, null);
        var rootTestScope = (Scope)tracer.StartActive("test.trace");

        var appSecContext = rootTestScope.Span.Context.TraceContext.AppSecRequestContext;
        appSecContext.CheckWAFError(raspErrorCode, true);
        appSecContext.CheckWAFError(wafErrorCode, false);
        appSecContext.CheckWAFError(wafErrorCode2, false);
        TraceTagCollection tags = new();
        appSecContext.CloseWebSpan(tags, rootTestScope.Span);
        tags.GetTag(Tags.WafError).Should().Be(expectedWafErrorCode?.ToString());
        tags.GetTag(Tags.RaspWafError).Should().Be(expectedRaspErrorCode?.ToString());
    }
}
