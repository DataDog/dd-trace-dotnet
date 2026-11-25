// <copyright file="ActivityStatusTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Activity;

public class ActivityStatusTests
{
    [Fact]
    public void Default_IsUnsetWithNullDescription()
    {
        var status = default(ActivityStatus);

        status.Code.Should().Be(ActivityStatusCode.Unset);
        status.Description.Should().BeNull();
    }

    [Fact]
    public void From_WhenError_ReadsStatusDescription()
    {
        var activityMock = new Mock<IActivity6>();
        activityMock.Setup(x => x.Status).Returns(ActivityStatusCode.Error);
        activityMock.Setup(x => x.StatusDescription).Returns("boom");

        var status = ActivityStatus.From(activityMock.Object);

        status.Code.Should().Be(ActivityStatusCode.Error);
        status.Description.Should().Be("boom");
        activityMock.VerifyGet(x => x.StatusDescription, Times.Once);
    }

    [Theory]
    [InlineData((int)ActivityStatusCode.Ok)]
    [InlineData((int)ActivityStatusCode.Unset)]
    public void From_WhenNotError_DoesNotReadStatusDescription(int activityStatusCode)
    {
        var code = (ActivityStatusCode)activityStatusCode;
        var activityMock = new Mock<IActivity6>();
        activityMock.Setup(x => x.Status).Returns(code);

        var status = ActivityStatus.From(activityMock.Object);

        status.Code.Should().Be(code);
        status.Description.Should().BeNull();
        activityMock.VerifyGet(x => x.StatusDescription, Times.Never);
    }
}
