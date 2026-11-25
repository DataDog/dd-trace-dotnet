// <copyright file="ActivityStatusWiringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Threading.Tasks;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
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
public class ActivityStatusWiringTests
{
    [Fact]
    public async Task Activity6StopDelegate_PassesTheActivityStatusToTheSpan()
    {
        await using var tracer = TracerHelper.Create(new TracerSettings(), agentWriter: Mock.Of<IAgentWriter>());
        TracerRestorerAttribute.SetTracer(tracer);

        using var source = new SD.ActivitySource(nameof(ActivityStatusWiringTests));
        using var listener = new SD.ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref SD.ActivityCreationOptions<SD.ActivityContext> _) => SD.ActivitySamplingResult.AllData,
        };
        SD.ActivitySource.AddActivityListener(listener);
        ActivityListenerHandler.OnShouldListenTo(source.DuckCast<IActivitySource>());

        // The same delegates ActivityListener.Initialize() emits for the Activity6 shape
        var onStarted = (Action<SD.Activity>)ActivityListenerDelegatesBuilder.CreateOnActivityStartedDelegate(typeof(SD.Activity), ActivityShape.Activity6);
        var onStopped = (Action<SD.Activity>)ActivityListenerDelegatesBuilder.CreateOnActivityStoppedDelegate(typeof(SD.Activity), ActivityShape.Activity6);

        using var activity = source.StartActivity("operation")!;
        onStarted(activity);
        var span = (Span)tracer.ActiveScope!.Span;

        activity.SetStatus(SD.ActivityStatusCode.Error, "something failed");
        activity.Stop();
        onStopped(activity);

        span.IsFinished.Should().BeTrue();
        span.Error.Should().BeTrue();
        span.GetTag(Tags.ErrorMsg).Should().Be("something failed");
        span.GetTag("otel.status_code").Should().Be("STATUS_CODE_ERROR");
    }
}
#endif
