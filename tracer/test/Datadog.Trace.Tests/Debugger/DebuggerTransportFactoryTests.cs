// <copyright file="DebuggerTransportFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Upload;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DebuggerTransportFactoryTests
{
    [Fact]
    public void ReturnsAgentlessTransport_WhenDynamicInstrumentationAgentlessConfigured()
    {
        var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()));
        var debuggerSettings = new DebuggerSettings(
            new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.DynamicInstrumentationAgentlessEnabled, "true" },
                { ConfigurationKeys.Debugger.DynamicInstrumentationProbeFile, "probes.json" },
                { ConfigurationKeys.ApiKey, "test-key" },
                { ConfigurationKeys.Site, "datadoghq.eu" }
            }),
            NullConfigurationTelemetry.Instance);

        var transport = DebuggerTransportFactory.CreateForDynamicInstrumentation(tracerSettings, debuggerSettings, NullDiscoveryService.Instance);

        transport.Should().NotBeNull();
        transport!.Value.IsAgentless.Should().BeTrue();
        transport.Value.DiscoveryService.Should().BeNull();
        transport.Value.StaticEndpoint.Should().Be(DebuggerTransportFactory.DefaultAgentlessRelativePath);
        transport.Value.ApiRequestFactory.GetEndpoint(transport.Value.StaticEndpoint).ToString().Should().Be("https://debugger-intake.datadoghq.eu/api/v2/debugger");
    }

    [Fact]
    public void ReturnsNull_WhenDynamicInstrumentationAgentlessEnabledWithoutApiKey()
    {
        var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()));
        var debuggerSettings = new DebuggerSettings(
            new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.DynamicInstrumentationAgentlessEnabled, "true" },
                { ConfigurationKeys.Debugger.DynamicInstrumentationProbeFile, "probes.json" }
            }),
            NullConfigurationTelemetry.Instance);

        var transport = DebuggerTransportFactory.CreateForDynamicInstrumentation(tracerSettings, debuggerSettings, NullDiscoveryService.Instance);

        transport.Should().BeNull();
    }

    private sealed class NullDiscoveryService : IDiscoveryService
    {
        public static NullDiscoveryService Instance { get; } = new();

        public void SubscribeToChanges(Action<AgentConfiguration> callback)
        {
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
        }

        public void SetCurrentConfigStateHash(string configStateHash)
        {
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
