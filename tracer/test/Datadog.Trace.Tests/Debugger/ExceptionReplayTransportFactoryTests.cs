// <copyright file="ExceptionReplayTransportFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
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
            var discovery = new TestDiscoveryService();

            var transport = ExceptionReplayTransportFactory.Create(tracerSettings, erSettings, discovery);

            transport.IsAgentless.Should().BeTrue();
            transport.StaticEndpoint.Should().Be("/custom/path");
            transport.ApiRequestFactory.GetEndpoint("/api/v2/debugger")
                     .ToString()
                     .Should().Be("https://custom-host.example.com/api/v2/debugger");
        }

        [Fact]
        public void HeaderInjectingFactory_AddsDebuggerHeaders()
        {
            var nestedType = typeof(ExceptionReplayTransportFactory)
                            .GetNestedType("HeaderInjectingApiRequestFactory", BindingFlags.NonPublic);
            nestedType.Should().NotBeNull();
            var ctor = nestedType!.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                new[] { typeof(IApiRequestFactory), typeof(string) },
                modifiers: null);
            ctor.Should().NotBeNull();

            var recordingFactory = new RecordingApiRequestFactory();
            var wrapper = (IApiRequestFactory)ctor!.Invoke(new object[] { recordingFactory, "secret-key" });

            wrapper.Create(new Uri("https://debugger-intake.example.com/api/v2/debugger"));

            var recordedRequest = recordingFactory.Requests.Single();
            recordedRequest.Headers.Should().ContainKey("DD-API-KEY").WhoseValue.Should().Be("secret-key");
            recordedRequest.Headers.Should().ContainKey("DD-EVP-ORIGIN").WhoseValue.Should().Be("dd-trace-dotnet");
            recordedRequest.Headers.Should().ContainKey("DD-REQUEST-ID");
            Guid.TryParse(recordedRequest.Headers["DD-REQUEST-ID"], out _).Should().BeTrue();
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

        private sealed class RecordingApiRequestFactory : IApiRequestFactory
        {
            public List<RecordingApiRequest> Requests { get; } = new();

            public string Info(Uri endpoint) => endpoint.ToString();

            public Uri GetEndpoint(string relativePath) => new($"https://placeholder{relativePath}");

            public IApiRequest Create(Uri endpoint)
            {
                var request = new RecordingApiRequest(endpoint);
                Requests.Add(request);
                return request;
            }

            public void SetProxy(System.Net.WebProxy proxy, System.Net.NetworkCredential credential)
            {
            }
        }

        private sealed class RecordingApiRequest : IApiRequest
        {
            public RecordingApiRequest(Uri endpoint)
            {
                Endpoint = endpoint;
            }

            public Uri Endpoint { get; }

            public Dictionary<string, string> Headers { get; } = new();

            public void AddHeader(string name, string value) => Headers[name] = value;

            public Task<IApiResponse> GetAsync() => throw new NotSupportedException();

            public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType) => throw new NotSupportedException();

            public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding) => throw new NotSupportedException();

            public Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary) => throw new NotSupportedException();

            public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None) => throw new NotSupportedException();
        }
    }
}
