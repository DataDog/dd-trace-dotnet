// <copyright file="PathHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using Datadog.FleetInstaller;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

public class PathHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("./installer")] // relative
    [InlineData("somewhere/../installer")] // relative
    [InlineData(@"c:\")] // root (no parent)
    public void ForInvalidPaths_ReturnsEmptyString(string homePath)
    {
        var actual = PathHelper.GetTelemetryForwarderPath(homePath);
        actual.Should().BeEmpty();
    }

    [Theory]
    [InlineData(@"c:\some\path\to\aplace", @"c:\some\path\to\installer\telemetry_forwarder.exe")]
    [InlineData(@"c:\some\path\..\to\aplace", @"c:\some\to\installer\telemetry_forwarder.exe")]
    [InlineData(@"D:\some\path\to\aplace.exe", @"D:\some\path\to\installer\telemetry_forwarder.exe")] // a bit questionable, but meh
    [InlineData(@"c:\library", @"c:\installer\telemetry_forwarder.exe")]
    [InlineData(@"f:\other-library", @"f:\installer\telemetry_forwarder.exe")]
    public void ForValidPaths_ReturnsExpectedPath(string homePath, string expected)
    {
        var actual = PathHelper.GetTelemetryForwarderPath(homePath);
        actual.Should().Be(expected);
    }
}

#endif
