// <copyright file="OverheadControllerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Sampling;
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
        var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);
        var instance = new OverheadController(iastSettings.MaxConcurrentRequests, iastSettings.RequestSampling);
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenSetSamplingTo100_AllOfRequestAreAcquired()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, 100 }
        });
        var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);
        var instance = new OverheadController(iastSettings.MaxConcurrentRequests, iastSettings.RequestSampling);
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
        var settings = new CustomSettingsForTests(new Dictionary<string, object>
        {
            { ConfigurationKeys.Iast.RequestSampling, 25 }
        });
        var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);
        var instance = new OverheadController(iastSettings.MaxConcurrentRequests, iastSettings.RequestSampling);
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenSetMaxConcurrentRequestsTo1_Only1ConcurrentRequestsIsAllowed()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.MaxConcurrentRequests, 1 },
            { ConfigurationKeys.Iast.RequestSampling, 100 }
        });
        var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);
        var instance = new OverheadController(iastSettings.MaxConcurrentRequests, iastSettings.RequestSampling);
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenSetMaxConcurrentRequestsTo2_Only2ConcurrentRequestsAreAllowed()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.MaxConcurrentRequests, 2 },
            { ConfigurationKeys.Iast.RequestSampling, 100 }
        });
        var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);
        var instance = new OverheadController(iastSettings.MaxConcurrentRequests, iastSettings.RequestSampling);
        Assert.True(instance.AcquireRequest());
        Assert.True(instance.AcquireRequest());
        Assert.False(instance.AcquireRequest());
        instance.ReleaseRequest();
        Assert.True(instance.AcquireRequest());
    }

    [Fact]
    public void GivenAnOverheadController_WhenWhenSetSamplingTo50AndSetMaxConcurrentRequestsTo2_ResultIsOk()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.MaxConcurrentRequests, 2 },
            { ConfigurationKeys.Iast.RequestSampling, 50 }
        });
        var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);
        var instance = new OverheadController(iastSettings.MaxConcurrentRequests, iastSettings.RequestSampling);
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
