// <copyright file="QuartzCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler;

public class QuartzCommonTests
{
    [Theory]
    [InlineData("job.name", "Quartz.Job.Execute", "execute helloJob")]
    [InlineData("quartz.job.name", "Quartz.Job.Execute", "execute helloJob")]
    [InlineData("job.name", "Quartz.Job.Veto", "veto helloJob")]
    [InlineData("quartz.job.name", "Quartz.Job.Veto", "veto helloJob")]
    [InlineData("job.name", "Quartz.JobStore.ScheduleJob", "schedule helloJob")]
    [InlineData("quartz.job.name", "Quartz.JobStore.ScheduleJob", "schedule helloJob")]
    [InlineData("job.name", "Quartz.JobStore.TriggeredJobComplete", "Quartz.JobStore.TriggeredJobComplete")]
    [InlineData("quartz.job.name", "Quartz.JobStore.TriggeredJobComplete", "Quartz.JobStore.TriggeredJobComplete")]
    public void EnhanceActivity5MetadataPreservesJobName(string tagName, string operationName, string expectedResource)
    {
        var activity = new Mock<IActivity5>();
        activity.SetupProperty(a => a.DisplayName, operationName);
        activity.SetupGet(a => a.TagObjects).Returns(new[] { new KeyValuePair<string, object>(tagName, "helloJob") });

        QuartzCommon.EnhanceActivityMetadata(activity.Object);

        activity.Object.DisplayName.Should().Be(expectedResource);
        activity.Verify(a => a.AddTag("operation.name", operationName), Times.Once);
    }

    [Theory]
    [InlineData("job.name", "Quartz.Job.Execute", "execute helloJob")]
    [InlineData("quartz.job.name", "Quartz.Job.Execute", "execute helloJob")]
    [InlineData("job.name", "Quartz.Job.Veto", "veto helloJob")]
    [InlineData("quartz.job.name", "Quartz.Job.Veto", "veto helloJob")]
    [InlineData("job.name", "Quartz.JobStore.ScheduleJob", "schedule helloJob")]
    [InlineData("quartz.job.name", "Quartz.JobStore.ScheduleJob", "schedule helloJob")]
    [InlineData("job.name", "Quartz.JobStore.TriggeredJobComplete", "Quartz.JobStore.TriggeredJobComplete")]
    [InlineData("quartz.job.name", "Quartz.JobStore.TriggeredJobComplete", "Quartz.JobStore.TriggeredJobComplete")]
    public void EnhanceActivityMetadataPreservesJobName(string tagName, string operationName, string expectedResource)
    {
        var activity = new Mock<IActivity>();
        activity.SetupGet(a => a.OperationName).Returns(operationName);
        activity.SetupGet(a => a.Tags).Returns(new[] { new KeyValuePair<string, string>(tagName, "helloJob") });

        QuartzCommon.EnhanceActivityMetadata(activity.Object);

        activity.Verify(a => a.AddTag("resource.name", expectedResource), Times.Once);
        activity.Verify(a => a.AddTag("operation.name", operationName), Times.Once);
    }
}
