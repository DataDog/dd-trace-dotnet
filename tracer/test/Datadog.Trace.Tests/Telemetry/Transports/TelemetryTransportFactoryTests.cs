// <copyright file="TelemetryTransportFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
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
    [MemberData(nameof(Data.Transports), MemberType = typeof(Data))]
    public void UsesCorrectTransports(bool agentProxyEnabled, bool agentlessEnabled, bool v2Enabled)
    {
        var telemetrySettings = new TelemetrySettings(
            telemetryEnabled: true,
            configurationError: null,
            agentlessSettings: agentlessEnabled ? new TelemetrySettings.AgentlessSettings(new Uri("http://localhost"), "SOME_API_KEY") : null,
            agentProxyEnabled: agentProxyEnabled,
            heartbeatInterval: TimeSpan.FromSeconds(1),
            dependencyCollectionEnabled: true,
            v2Enabled: v2Enabled);

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

    public static class Data
    {
        private static readonly bool[] TrueFalse = { true, false };

        public static IEnumerable<object[]> Transports
            => from agentProxyEnabled in TrueFalse
               from agentlessEnabled in TrueFalse
               from v2Enabled in TrueFalse
               select new object[] { agentProxyEnabled, agentlessEnabled, v2Enabled };
    }
}
