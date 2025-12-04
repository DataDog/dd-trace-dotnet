// <copyright file="ExceptionReplayTransportFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Specialized;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.TestHelpers.TransportHelpers;
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

            var transport = ExceptionReplayTransportFactory.Create(tracerSettings, erSettings, NullDiscoveryService.Instance);
            transport.Should().NotBeNull();

            transport!.Value.IsAgentless.Should().BeFalse();
            transport.Value.DiscoveryService.Should().Be(NullDiscoveryService.Instance);
            transport.Value.StaticEndpoint.Should().BeNull();
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

            var transport = ExceptionReplayTransportFactory.Create(tracerSettings, erSettings, NullDiscoveryService.Instance);
            transport.Should().NotBeNull();

            transport!.Value.IsAgentless.Should().BeTrue();
            transport.Value.DiscoveryService.Should().BeNull();
            transport.Value.StaticEndpoint.Should().Be("/api/v2/debugger");
        }

        [Fact]
        public void AgentlessTransport_UsesOverrideUrl()
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection()));
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.Debugger.ExceptionReplayAgentlessEnabled, "true" },
                { ConfigurationKeys.ApiKey, "test-key" },
                { ConfigurationKeys.Debugger.ExceptionReplayAgentlessUrl, "https://custom-host.example.com/custom/path" }
            };
            var erSettings = new ExceptionReplaySettings(new NameValueConfigurationSource(collection), NullConfigurationTelemetry.Instance);

            var transport = ExceptionReplayTransportFactory.Create(tracerSettings, erSettings, NullDiscoveryService.Instance);
            transport.Should().NotBeNull();

            transport!.Value.IsAgentless.Should().BeTrue();
            transport.Value.StaticEndpoint.Should().Be("/custom/path");
            transport.Value.ApiRequestFactory.GetEndpoint("/api/v2/debugger")
                     .ToString()
                     .Should().Be("https://custom-host.example.com/api/v2/debugger");
        }

        [Fact]
        public void HeaderInjectingFactory_AddsDebuggerHeaders()
        {
            var recordingFactory = new TestRequestFactory(new Uri("https://placeholder"));
            var wrapper = new ExceptionReplayTransportFactory.HeaderInjectingApiRequestFactory(recordingFactory, "secret-key");
            var request = (TestApiRequest)wrapper.Create(new Uri("https://debugger-intake.example.com/api/v2/debugger"));

            request.ExtraHeaders.Should().ContainKey("DD-API-KEY").WhoseValue.Should().Be("secret-key");
            request.ExtraHeaders.Should().ContainKey("DD-EVP-ORIGIN").WhoseValue.Should().Be("dd-trace-dotnet");
            request.ExtraHeaders.Should().ContainKey("DD-REQUEST-ID");
            Guid.TryParse(request.ExtraHeaders["DD-REQUEST-ID"], out _).Should().BeTrue();
        }
    }
}
