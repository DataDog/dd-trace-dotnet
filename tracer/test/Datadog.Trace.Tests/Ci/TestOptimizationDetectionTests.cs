// <copyright file="TestOptimizationDetectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class TestOptimizationDetectionTests : SettingsTestsBase
{
    [Theory]
    [MemberData(nameof(NullableBooleanTestCases), null)]
    public void Enabled(string value, bool? expected)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.CIVisibility.Enabled, value));
        var settings = TestOptimizationDetection.IsEnabled(source, NullConfigurationTelemetry.Instance, DatadogSerilogLogger.NullLogger);

        settings.ExplicitEnabled.Should().Be(expected);
    }
}
