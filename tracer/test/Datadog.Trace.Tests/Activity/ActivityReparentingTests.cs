// <copyright file="ActivityReparentingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;
using SD = System.Diagnostics;

namespace Datadog.Trace.Tests.Activity;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class ActivityReparentingTests
{
    private const string SourceName = "ActivityReparentingTests";

    // A parent started 5s after the active span is well beyond the 50ms clock-skew tolerance, so it counts as newer
    [Theory]
    [InlineData(-5, true)]
    [InlineData(5, false)]
    public async Task ChildIsReparentedUnderActiveSpan_OnlyWhenItsParentIsOlder(int parentStartOffsetSeconds, bool expectReparented)
    {
        var parentStartOffset = TimeSpan.FromSeconds(parentStartOffsetSeconds);
        await using var tracer = TracerHelper.Create(new TracerSettings(), agentWriter: Mock.Of<IAgentWriter>());
        TracerRestorerAttribute.SetTracer(tracer);

        using var activeScope = (Scope)tracer.StartActive("active-span");
        var activeSpan = activeScope.Span;

        var parent = new SD.Activity("parent");
        parent.SetIdFormat(SD.ActivityIdFormat.W3C);
        parent.Start();
        parent.SetStartTime(activeSpan.StartTime.UtcDateTime + parentStartOffset);

        var child = new SD.Activity("child");
        child.SetIdFormat(SD.ActivityIdFormat.W3C);
        child.Start();
        child.Parent.Should().BeSameAs(parent);

        var handler = new DefaultActivityHandler();
        handler.ActivityStarted(SourceName, child.DuckCast<IActivity>());

        try
        {
            // Reparenting rewrites the Activity onto the active span, so later propagation continues the Datadog trace
            if (expectReparented)
            {
                child.TraceId.ToHexString().Should().Be(activeSpan.Context.RawTraceId);
                child.ParentSpanId.ToHexString().Should().Be(activeSpan.Context.RawSpanId);
            }
            else
            {
                child.TraceId.Should().Be(parent.TraceId);
                child.ParentSpanId.Should().Be(parent.SpanId);
            }
        }
        finally
        {
            child.Stop();
            handler.StopActivity(SourceName, child);

            parent.Stop();
        }
    }
}
