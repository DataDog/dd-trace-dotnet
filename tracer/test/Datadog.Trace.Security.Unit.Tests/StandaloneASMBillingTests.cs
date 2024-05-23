// <copyright file="StandaloneASMBillingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Security.Unit.Tests.Iast;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class StandaloneASMBillingTests
{
    [Fact]
    public void GivenAppsecStandaloneEnabledSettings_WhenEnabled_IsReallyEnabled()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.ExperimentalAppsecStandaloneEnabled, true }
        });
        var tracerSettings = new TracerSettings(settings, NullConfigurationTelemetry.Instance);
        Assert.True(tracerSettings.ExperimentalAppSecStandaloneEnabled);
    }

    [Fact]
    public void GivenAppsecStandaloneEnabledSettings_WhenEnabled_StatsComputationIsDisabled()
    {
        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.ExperimentalAppsecStandaloneEnabled, true },
            { ConfigurationKeys.StatsComputationEnabled, true }
        });
        var tracerSettings = new TracerSettings(settings, NullConfigurationTelemetry.Instance);

        // Should ignore the configuration set by the customer
        Assert.False(tracerSettings.StatsComputationEnabled);
        Assert.False(tracerSettings.StatsComputationEnabledInternal);
    }
}
