// <copyright file="ExceptionReplayTransportFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class ExceptionReplayTransportFactoryTests
    {
        [Fact]
        public void ReturnsAgentTransport_WhenAgentlessDisabled()
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection()));
            var erSettings = new ExceptionReplaySettings(new NameValueConfigurationSource(new NameValueCollection()), NullConfigurationTelemetry.Instance);
            var discovery = new TestDiscoveryService();

            var transport = ExceptionReplayTransportFactory.Create(tracerSettings, erSettings, discovery);

            transport.IsAgentless.Should().BeFalse();
            transport.DiscoveryService.Should().Be(discovery);
            transport.StaticEndpoint.Should().BeNull();
        }

        [Fact]
        public void ReturnsAgentlessTransport_WhenConfigured()
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection()));
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.Debugger.ExceptionReplayAgentlessEnabled, "true" },
                { ConfigurationKeys.ApiKey, "test-key" }
            };
            var erSettings = new ExceptionReplaySettings(new NameValueConfigurationSource(collection), NullConfigurationTelemetry.Instance);
            var discovery = new TestDiscoveryService();

            var transport = ExceptionReplayTransportFactory.Create(tracerSettings, erSettings, discovery);

            transport.IsAgentless.Should().BeTrue();
            transport.DiscoveryService.Should().BeNull();
            transport.StaticEndpoint.Should().Be("/api/v2/debugger");
        }

        private sealed class TestDiscoveryService : IDiscoveryService
        {
            public void SubscribeToChanges(System.Action<AgentConfiguration> callback)
            {
            }

            public void RemoveSubscription(System.Action<AgentConfiguration> callback)
            {
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
    }
}
