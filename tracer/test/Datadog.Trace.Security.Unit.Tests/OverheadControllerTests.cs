// <copyright file="OverheadControllerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Sampling;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class OverheadControllerTests
{
    [Fact]
    public void GiveAnOverheadController_WhenSetSamplingTo50_HalfOfRequestAreAcquired()
    {
        var instance = new OverheadController(2, 50);
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenSetSamplingTo100_AllOfRequestAreAcquired()
    {
        var instance = new OverheadController(2, 100);
        Assert.True(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenSetSamplingTo25_AQuarterOfRequestAreAcquired()
    {
        var instance = new OverheadController(2, 25);
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenSetMaxConcurrentRequestsTo1_Only1ConcurrentRequestsIsAllowed()
    {
        var instance = new OverheadController(1, 100);
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenSetMaxConcurrentRequestsTo2_Only2ConcurrentRequestsAreAllowed()
    {
        var instance = new OverheadController(2, 100);
        Assert.True(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenWhenSetSamplingTo50AndSetMaxConcurrentRequestsTo2_ResultIsOk()
    {
        var instance = new OverheadController(2, 50);
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.False(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        instance.Reset();
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
    }
}
