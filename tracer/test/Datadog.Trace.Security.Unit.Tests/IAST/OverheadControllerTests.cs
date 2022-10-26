// <copyright file="OverheadControllerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Security.Unit.Tests.IAST;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast;

public class OverheadControllerTests
{
    [Fact]
    public void GiveAnOverheadController_WhenSetSamplingTo50_HalfOfRequestAreAcquired()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, 50 }
        });
        var instance = new OverheadController(new IastSettings(settings));
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
    }

    [Fact]
    public void GiveAnOverheadController_WhenSetSamplingTo100_AllOfRequestAreAcquired()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, 100 }
        });
        var instance = new OverheadController(new IastSettings(settings));
        Assert.True(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
    }

    [Fact]
    public void GiveAnOverheadController_WhenSetSamplingTo25_AQuarterOfRequestAreAcquired()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, 25 }
        });
        var instance = new OverheadController(new IastSettings(settings));
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
    }
}
