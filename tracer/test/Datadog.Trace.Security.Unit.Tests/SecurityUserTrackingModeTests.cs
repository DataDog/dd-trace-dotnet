// <copyright file="SecurityUserTrackingModeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class SecurityUserTrackingModeTests
{
    // This table specifies how local and remote config should be combined.
    // Remote config should override local config in this case.
    //
    // Local config     | Remote config     | Resulting collection mode
    // -----------------+-------------------+--------------------------
    // undefined        | undefined         | identification
    // any              | unknown value     | disabled
    // any              | disabled          | disable
    // any              | identification    | identification
    // any              | anonymization     | anonymization
    // unknown value    | undefined         | disabled
    // disabled         | undefined         | disabled
    // ident            | undefined         | identification
    // anon             | undefined         | anonymization
    //
    // The library doesn't use these values directly, but two booleans calculated from them:
    //  - IsTrackUserEventsEnabled
    //  - IsAnonUserTrackingMode
    // This test suite demonstrates that these two booleans are correctly calculated from the above table.
    // Local values can't be null, as the default is calculated within the framework for local config
    // telemetry.
    // Remote config can be null, and this tell us remote config is not active.

    [Theory]
    [InlineData("sdfqsf", SecuritySettings.UserTrackingIdentMode, false)]
    [InlineData(SecuritySettings.UserTrackingDisabled, SecuritySettings.UserTrackingIdentMode, false)]
    [InlineData(SecuritySettings.UserTrackingIdentMode, SecuritySettings.UserTrackingIdentMode, true)]
    [InlineData(SecuritySettings.UserTrackingAnonMode, SecuritySettings.UserTrackingIdentMode, true)]
    [InlineData(null, "dsfqsh", false)]
    [InlineData(null, SecuritySettings.UserTrackingDisabled, false)]
    [InlineData(null, SecuritySettings.UserTrackingIdentMode, true)]
    [InlineData(null, SecuritySettings.UserTrackingAnonMode, true)]
    public void TestCalculateIsTrackUserEventsEnabled(string remote, string local, bool expected)
    {
        var result = Datadog.Trace.AppSec.Security.CalculateIsTrackUserEventsEnabled(remote, local);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("sdfqsf", SecuritySettings.UserTrackingIdentMode, false)]
    [InlineData(SecuritySettings.UserTrackingDisabled, SecuritySettings.UserTrackingIdentMode, false)]
    [InlineData(SecuritySettings.UserTrackingIdentMode, SecuritySettings.UserTrackingIdentMode, false)]
    [InlineData(SecuritySettings.UserTrackingAnonMode, SecuritySettings.UserTrackingIdentMode, true)]
    [InlineData(null, "dsfqsh", false)]
    [InlineData(null, SecuritySettings.UserTrackingDisabled, false)]
    [InlineData(null, SecuritySettings.UserTrackingIdentMode, false)]
    [InlineData(null, SecuritySettings.UserTrackingAnonMode, true)]
    public void TestCalculateIsAnonUserTrackingMode(string remote, string local, bool expected)
    {
        var result = Datadog.Trace.AppSec.Security.CalculateIsAnonUserTrackingMode(remote, local);

        result.Should().Be(expected);
    }
}
