// <copyright file="TelemetryTransportFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Transports;

public class TelemetryTransportFactoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UsesCorrectTransports(bool agentProxyEnabled, bool agentlessEnabled)
    {
        var telemetrySettings = new TelemetrySettings(
            telemetryEnabled: true,
            configurationError: null,
            agentlessSettings: agentlessEnabled ? new TelemetrySettings.AgentlessSettings(new Uri("http://localhost"), "SOME_API_KEY") : null,
            agentProxyEnabled: agentProxyEnabled);

        var exporterSettings = new ImmutableExporterSettings(new ExporterSettings());

        var transports = TelemetryTransportFactory.Create(telemetrySettings, exporterSettings);

        using var s = new AssertionScope();
        if (agentProxyEnabled)
        {
            transports.Should().ContainSingle(x => x is AgentTelemetryTransport);
            transports[0]?.Should().BeOfType<AgentTelemetryTransport>();
        }

        if (agentlessEnabled)
        {
            transports.Should().ContainSingle(x => x is AgentlessTelemetryTransport);
        }
    }
}
