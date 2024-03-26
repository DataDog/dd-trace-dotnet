// <copyright file="TelemetryTransportManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Tests.Agent;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

public class TelemetryTransportManagerTests
{
    [Fact]
    public async Task WhenHaveSuccess_ReturnsTrue()
    {
        const int requestCount = 10;
        var telemetryPushResults = Enumerable.Repeat(TelemetryPushResult.Success, requestCount).ToArray();
        var transports = new TelemetryTransports(new TestTransport(telemetryPushResults), null);
        var transportManager = new TelemetryTransportManager(transports, new DiscoveryServiceMock());

        for (var i = 0; i < requestCount; i++)
        {
            var telemetryPushResult = await transportManager.TryPushTelemetry(null!);
            telemetryPushResult.Should().Be(true);
        }
    }

    [Theory]
    [InlineData((int)TelemetryPushResult.FatalError)]
    [InlineData((int)TelemetryPushResult.TransientFailure)]
    public async Task WhenHaveFailure_ReturnsFalse(int result)
    {
        const int requestCount = 10;
        var telemetryPushResults = Enumerable.Repeat((TelemetryPushResult)result, requestCount).ToArray();
        var transports = new TelemetryTransports(new TestTransport(telemetryPushResults), null);
        var transportManager = new TelemetryTransportManager(transports, new DiscoveryServiceMock());

        for (var i = 0; i < requestCount; i++)
        {
            var telemetryPushResult = await transportManager.TryPushTelemetry(null!);
            telemetryPushResult.Should().Be(false);
        }
    }

    [Fact]
    public async Task TestTransport_ThrowsIfCalledTooManyTimes()
    {
        var transport1 = new TestTransport(TelemetryPushResult.TransientFailure);
        var transports = new TelemetryTransports(transport1, null);
        var transportManager = new TelemetryTransportManager(transports, new DiscoveryServiceMock());

        await transportManager.TryPushTelemetry(null!);

        var call = async () => await transportManager.TryPushTelemetry(null!);
        await call.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WhenHaveFailure_SwitchesTransport()
    {
        var transport1 = new TestTransport(TelemetryPushResult.TransientFailure, TelemetryPushResult.Success);
        var transport2 = new TestTransport(TelemetryPushResult.TransientFailure);
        var transports = new TelemetryTransports(transport1, transport2);
        var transportManager = new TelemetryTransportManager(transports, new DiscoveryServiceMock());

        var fail1 = await transportManager.TryPushTelemetry(null!);
        var fail2 = await transportManager.TryPushTelemetry(null!);
        var success = await transportManager.TryPushTelemetry(null!);

        fail1.Should().BeFalse();
        fail2.Should().BeFalse();
        success.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void WhenOnlyAgentAvailable_AlwaysUsesAgent(bool? initiallyAvailableInDiscovery)
    {
        var transports = new TelemetryTransports(
            agentTransport: new TestTransport(),
            agentlessTransport: null);
        var discoveryService = new DiscoveryServiceMock();
        var manager = new TelemetryTransportManager(transports, discoveryService);

        if (initiallyAvailableInDiscovery == true)
        {
            discoveryService.TriggerChange();
        }
        else if (initiallyAvailableInDiscovery == false)
        {
            discoveryService.TriggerChange(telemetryProxyEndpoint: null);
        }

        // initial value
        var nextTransport = manager.GetNextTransport(null);
        nextTransport.Should().Be(transports.AgentTransport);

        // on error
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentTransport);

        // agent no longer available
        discoveryService.TriggerChange(telemetryProxyEndpoint: null);
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentTransport);

        // agent available again
        discoveryService.TriggerChange();
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentTransport);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void WhenOnlyAgentlessAvailable_AlwaysUsesAgentless(bool? initiallyAvailableInDiscovery)
    {
        var transports = new TelemetryTransports(agentTransport: null, agentlessTransport: new TestTransport());
        var discoveryService = new DiscoveryServiceMock();
        var manager = new TelemetryTransportManager(transports, discoveryService);

        if (initiallyAvailableInDiscovery == true)
        {
            discoveryService.TriggerChange();
        }
        else if (initiallyAvailableInDiscovery == false)
        {
            discoveryService.TriggerChange(telemetryProxyEndpoint: null);
        }

        // initial value
        var nextTransport = manager.GetNextTransport(null);
        nextTransport.Should().Be(transports.AgentlessTransport);

        // on failure
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentlessTransport);

        // agent no longer available
        discoveryService.TriggerChange(telemetryProxyEndpoint: null);
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentlessTransport);

        // agent available again
        discoveryService.TriggerChange();
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentlessTransport);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WhenBothAvailable_AndInitiallyAvailableOrUnknownDiscovery_UsesAgent(bool notifyAvailable)
    {
        var transports = new TelemetryTransports(agentTransport: new TestTransport(), agentlessTransport: new TestTransport());
        var discoveryService = new DiscoveryServiceMock();
        var manager = new TelemetryTransportManager(transports, discoveryService);

        if (notifyAvailable)
        {
            discoveryService.TriggerChange();
        }

        // initial value
        var nextTransport = manager.GetNextTransport(null);
        nextTransport.Should().Be(transports.AgentTransport);
    }

    [Fact]
    public void WhenBothAvailable_AndInitiallyUnAvailable_UsesAgentless()
    {
        var transports = new TelemetryTransports(agentTransport: new TestTransport(), agentlessTransport: new TestTransport());
        var discoveryService = new DiscoveryServiceMock();
        var manager = new TelemetryTransportManager(transports, discoveryService);

        discoveryService.TriggerChange(telemetryProxyEndpoint: null);

        // initial value
        var nextTransport = manager.GetNextTransport(null);
        nextTransport.Should().Be(transports.AgentlessTransport);
    }

    [Fact]
    public void WhenBothAvailable_UsesNextExpectedTransport()
    {
        var transports = new TelemetryTransports(agentTransport: new TestTransport(), agentlessTransport: new TestTransport());
        var discoveryService = new DiscoveryServiceMock();
        var manager = new TelemetryTransportManager(transports, discoveryService);

        // initial value
        var nextTransport = manager.GetNextTransport(null);
        nextTransport.Should().Be(transports.AgentTransport);

        // we now know agent is available, but it failed, so switch to agentless
        discoveryService.TriggerChange();
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentlessTransport);

        // agentless failed, and agent is available, so switch to agent
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentTransport);

        // agent failed, so switch back to agentless
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentlessTransport);

        // Now know agent is not available, agentless failed, but stick to agentless
        discoveryService.TriggerChange(telemetryProxyEndpoint: null);
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentlessTransport);

        // Same as above
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentlessTransport);

        // Agent is available again, so switch to agent
        discoveryService.TriggerChange();
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentTransport);

        // And we're back to the starting point again
        nextTransport = manager.GetNextTransport(nextTransport);
        nextTransport.Should().Be(transports.AgentlessTransport);
    }

    internal class TestTransport : ITelemetryTransport
    {
        private readonly TelemetryPushResult[] _results;
        private int _current = -1;

        public TestTransport(params TelemetryPushResult[] results)
        {
            _results = results;
        }

        public Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
        {
            _current++;
            if (_current >= _results.Length)
            {
                throw new InvalidOperationException("Transport received unexpected request");
            }

            return Task.FromResult(_results[_current]);
        }

        public string GetTransportInfo() => nameof(TestTransport);
    }
}
