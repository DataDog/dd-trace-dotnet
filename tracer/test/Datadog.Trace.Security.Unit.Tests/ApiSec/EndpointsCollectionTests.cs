// <copyright file="EndpointsCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Security.Unit.Tests.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.ApiSec;

public class EndpointsCollectionTests
{
    [Fact]
    public void GivenConfiguration_WhenApiSecEnabledAndEndpointsCollectionEnabled_IsEndpointsCollected()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>
        {
            { ConfigurationKeys.AppSec.ApiSecurityEnabled, true },
            { ConfigurationKeys.AppSec.ApiSecurityEndpointCollectionEnabled, true }
        });
        var security = new SecuritySettings(settings, NullConfigurationTelemetry.Instance);
        var apisec = new ApiSecurity(security);

        apisec.CanCollectEndpoints().Should().BeTrue();
        apisec.GetEndpointsCollectionMessageLimit().Should().Be(300);
    }

    [Fact]
    public void GivenConfiguration_WhenApiSecDisabledAndEndpointsCollectionEnabled_IsEndpointsCollected()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>
        {
            { ConfigurationKeys.AppSec.ApiSecurityEnabled, false },
            { ConfigurationKeys.AppSec.ApiSecurityEndpointCollectionEnabled, true }
        });
        var security = new SecuritySettings(settings, NullConfigurationTelemetry.Instance);
        var apisec = new ApiSecurity(security);

        apisec.CanCollectEndpoints().Should().BeTrue();
    }

    [Fact]
    public void GivenConfiguration_WhenEndpointsCollectionDisabled_IsEndpointsNotCollected()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>
        {
            { ConfigurationKeys.AppSec.ApiSecurityEndpointCollectionEnabled, false }
        });
        var security = new SecuritySettings(settings, NullConfigurationTelemetry.Instance);
        var apisec = new ApiSecurity(security);

        apisec.CanCollectEndpoints().Should().BeFalse();
    }

    [Fact]
    public void GivenMaxLimitConfiguration_EndpointsCollectedNumberShouldChange()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>
        {
            { ConfigurationKeys.AppSec.ApiSecurityEndpointCollectionMessageLimit, 10 }
        });
        var security = new SecuritySettings(settings, NullConfigurationTelemetry.Instance);
        var apisec = new ApiSecurity(security);

        apisec.GetEndpointsCollectionMessageLimit().Should().Be(10);
    }

    [Fact]
    public void GivenInvalidMaxLimitConfiguration_EndpointsCollectedNumberShouldBeDefault()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>
        {
            { ConfigurationKeys.AppSec.ApiSecurityEndpointCollectionMessageLimit, -555 }
        });
        var security = new SecuritySettings(settings, NullConfigurationTelemetry.Instance);
        var apisec = new ApiSecurity(security);

        apisec.GetEndpointsCollectionMessageLimit().Should().Be(300);
    }
}
