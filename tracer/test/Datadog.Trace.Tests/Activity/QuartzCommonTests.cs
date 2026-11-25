// <copyright file="QuartzCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER

using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Xunit;
using SD = System.Diagnostics;

namespace Datadog.Trace.Tests.Activity;

public class QuartzCommonTests
{
    [Theory]
    [InlineData("Quartz.Job.Execute", SD.ActivityKind.Internal)]
    [InlineData("Quartz.Job.Veto", SD.ActivityKind.Internal)]
    [InlineData("Quartz.JobStore.ScheduleJob", SD.ActivityKind.Client)]
    public void SetActivityKind_OverridesKindForJobActivities(string operationName, SD.ActivityKind expected)
    {
        using var source = new SD.ActivitySource(nameof(QuartzCommonTests));
        using var listener = new SD.ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref SD.ActivityCreationOptions<SD.ActivityContext> _) => SD.ActivitySamplingResult.AllData,
        };
        SD.ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity(operationName, SD.ActivityKind.Client)!;

        QuartzCommon.SetActivityKind(activity.DuckCast<IActivity5>());

        activity.Kind.Should().Be(expected);
    }
}
#endif
