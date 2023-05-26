// <copyright file="GeneratePublicApiConfigurationCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

[TelemetryRestorer]
public class GeneratePublicApiConfigurationCollectionTests
{
    [Fact]
    public void RecordsChangesMadeInCode()
    {
        var config = new ConfigurationTelemetry();
        TelemetryFactory.SetConfigForTesting(config);

        const int limit = 200;
        var tracerSettings = new TracerSettings(NullConfigurationSource.Instance, config);
        var previousData = config.GetData(); // defaults
        tracerSettings.MaxTracesSubmittedPerSecond = limit;

        var remainingData = config.GetData();
        var configKeyValue = remainingData.Should().ContainSingle().Subject;
        configKeyValue.Name.Should().Be(ConfigurationKeys.TraceRateLimit);
        configKeyValue.Value.Should().Be(limit);
        configKeyValue.Origin.Should().Be(ConfigurationOrigins.Code.ToStringFast());
    }

    [Fact]
    public void DoesntRecordChangesMadeToBackingProperties()
    {
        var config = new ConfigurationTelemetry();
        TelemetryFactory.SetConfigForTesting(config);

        const int limit = 200;
        var tracerSettings = new TracerSettings(NullConfigurationSource.Instance, config);
        var previousData = config.GetData(); // defaults
        tracerSettings.MaxTracesSubmittedPerSecondInternal = limit; // backing property

        config.GetData().Should().BeNullOrEmpty();
    }
}
