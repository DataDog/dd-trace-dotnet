// <copyright file="TelemetryTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class TelemetryTransportTests
    {
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CanSendTelemetry(bool useV2)
        {
            using var agent = new MockTelemetryAgent(TcpPortProvider.GetOpenPort());
            var telemetryUri = new Uri($"http://localhost:{agent.Port}");

            // Uses framework specific transport
            var transport = GetAgentOnlyTransport(telemetryUri);
            var data =  GetSampleData(useV2);
            var result = await SendData(transport, data);

            result.Should().Be(TelemetryPushResult.Success);
            var received = agent.WaitForLatestTelemetry(x => x.SeqId == data.SeqId);

            received.Should().NotBeNull();

            // check some basic values
            received.SeqId.Should().Be(data.SeqId);
            if (data is TelemetryWrapper.V1 expectedV1)
            {
                var v1 = received.Should().BeOfType<TelemetryWrapper.V1>().Subject;
                v1.Data.Application.Env.Should().Be(expectedV1.Data.Application.Env);
                v1.Data.Application.ServiceName.Should().Be(expectedV1.Data.Application.ServiceName);
            }
            else if (data is TelemetryWrapper.V2 expectedV2)
            {
                var v2 = received.Should().BeOfType<TelemetryWrapper.V2>().Subject;
                v2.Data.Application.Env.Should().Be(expectedV2.Data.Application.Env);
                v2.Data.Application.ServiceName.Should().Be(expectedV2.Data.Application.ServiceName);
            }
            else
            {
                throw new InvalidOperationException("Input data was unknown type" + data);
            }
        }

        [SkippableTheory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task SetsRequiredHeaders(bool agentless, bool useV2)
        {
            const string apiKey = "some key";
            using var agent = new MockTelemetryAgent(TcpPortProvider.GetOpenPort());
            var telemetryUri = new Uri($"http://localhost:{agent.Port}");

            // Uses framework specific transport
            var transport = agentless
                                ? GetAgentlessOnlyTransport(telemetryUri, apiKey)
                                : GetAgentOnlyTransport(telemetryUri);

            var data = GetSampleData(useV2);
            var result = await SendData(transport, data);

            result.Should().Be(TelemetryPushResult.Success);
            var received = agent.WaitForLatestTelemetry(x => x.SeqId == data.SeqId);

            received.Should().NotBeNull();

            var allExpected = new Dictionary<string, string>
            {
                { "DD-Telemetry-API-Version", useV2 ? TelemetryConstants.ApiVersionV2 : TelemetryConstants.ApiVersionV1 },
                { "Content-Type", "application/json" },
                { "Content-Length", null },
                { "DD-Telemetry-Request-Type", "app-heartbeat" },
                { "DD-Client-Library-Language", "dotnet" },
                { "DD-Client-Library-Version", TracerConstants.AssemblyVersion },
            };

            if (ContainerMetadata.GetContainerId() is { } containerId)
            {
                allExpected.Add("Datadog-Container-ID", containerId);
            }

            if (agentless)
            {
                allExpected.Add(TelemetryConstants.ApiKeyHeader, apiKey);
            }

            foreach (var headers in agent.RequestHeaders)
            {
                foreach (var header in allExpected)
                {
                    headers.AllKeys.Should().Contain(header.Key);
                    if (header.Value is not null)
                    {
                        headers[header.Key].Should().Be(header.Value);
                    }
                }
            }
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WhenNoListener_ReturnsFatalError(bool useV2)
        {
            // Nothing listening on this port (currently)
            var port = TcpPortProvider.GetOpenPort();
            var telemetryUri = new Uri($"http://localhost:{port}");

            // Uses framework specific transport
            var transport = GetAgentOnlyTransport(telemetryUri);
            var data = GetSampleData(useV2);
            var result = await SendData(transport, data);

            result.Should().Be(TelemetryPushResult.FatalError);
        }

        [SkippableTheory]
        [MemberData(nameof(Data.GetStatusCodes), MemberType = typeof(Data))]
        public async Task ReturnsExpectedPushResultForStatusCode(int responseCode, int expectedPushResult, bool useV2)
        {
            using var agent = new ErroringTelemetryAgent(
                responseCode: responseCode,
                port: TcpPortProvider.GetOpenPort());
            var telemetryUri = new Uri($"http://localhost:{agent.Port}");
            var transport = GetAgentOnlyTransport(telemetryUri);
            var data = GetSampleData(useV2);
            var result = await SendData(transport, data);

            result.Should().Be((TelemetryPushResult)expectedPushResult);
        }

        private static async Task<TelemetryPushResult> SendData(ITelemetryTransport transport, TelemetryWrapper data)
        {
            if (data is TelemetryWrapper.V1 v1Data)
            {
                return await transport.PushTelemetry(v1Data.Data);
            }
            else if (data is TelemetryWrapper.V2 v2Data)
            {
                return await transport.PushTelemetry(v2Data.Data);
            }
            else
            {
                throw new InvalidOperationException("Unknown wrapper type: " + data.GetType());
            }
        }

        private static TelemetryWrapper GetSampleData(bool useV2)
        {
            return useV2
                       ? new TelemetryWrapper.V2(
                           new(
                               requestType: TelemetryRequestTypes.AppHeartbeat,
                               tracerTime: 1234,
                               runtimeId: "some-value",
                               seqId: 23,
                               application: new ApplicationTelemetryDataV2(
                                   serviceName: "TelemetryTransportTests",
                                   env: "TracerTelemetryTest",
                                   serviceVersion: "123",
                                   tracerVersion: TracerConstants.AssemblyVersion,
                                   languageName: "dotnet",
                                   languageVersion: "1.2.3",
                                   runtimeName: "dotnet",
                                   runtimeVersion: "7.0.3"),
                               host: new HostTelemetryDataV2("SOME_HOST", "Windows", "x64"),
                               payload: null))
                       : new TelemetryWrapper.V1(
                           new(
                               requestType: TelemetryRequestTypes.AppHeartbeat,
                               tracerTime: 1234,
                               runtimeId: "some-value",
                               seqId: 23,
                               application: new ApplicationTelemetryData(
                                   serviceName: "TelemetryTransportTests",
                                   env: "TracerTelemetryTest",
                                   tracerVersion: TracerConstants.AssemblyVersion,
                                   languageName: "dotnet",
                                   languageVersion: "1.2.3"),
                               host: new HostTelemetryData(),
                               payload: null));
        }

        private static ITelemetryTransport GetAgentOnlyTransport(Uri telemetryUri)
        {
            var transport = TelemetryTransportFactory.Create(
                new TelemetrySettings(telemetryEnabled: true, configurationError: null, agentlessSettings: null, agentProxyEnabled: true, heartbeatInterval: HeartbeatInterval, dependencyCollectionEnabled: true, v2Enabled: false, metricsEnabled: false),
                new ImmutableExporterSettings(new ExporterSettings { AgentUri = telemetryUri }));
            transport.Should().HaveCount(1);
            transport[0].Should().BeOfType<AgentTelemetryTransport>();
            return transport[0];
        }

        private static ITelemetryTransport GetAgentlessOnlyTransport(Uri telemetryUri, string apiKey)
        {
            var agentlessSettings = new TelemetrySettings.AgentlessSettings(telemetryUri, apiKey);

            var transport = TelemetryTransportFactory.Create(
                new TelemetrySettings(telemetryEnabled: true, configurationError: null, agentlessSettings, agentProxyEnabled: false, heartbeatInterval: HeartbeatInterval, dependencyCollectionEnabled: true, v2Enabled: false, metricsEnabled: false),
                new ImmutableExporterSettings(new ExporterSettings()));

            transport.Should().HaveCount(1);
            transport[0].Should().BeOfType<AgentlessTelemetryTransport>();
            return transport[0];
        }

        private static ITelemetryTransport GetAgentlessOnlyTransport(Uri telemetryUri, string apiKey)
        {
            var agentlessSettings = new TelemetrySettings.AgentlessSettings(telemetryUri, apiKey);

            var transport = TelemetryTransportFactory.Create(
                new TelemetrySettings(telemetryEnabled: true, configurationError: null, agentlessSettings, agentProxyEnabled: false, heartbeatInterval: HeartbeatInterval),
                new ImmutableExporterSettings(new ExporterSettings()));

            transport.Should().HaveCount(1);
            transport[0].Should().BeOfType<AgentlessTelemetryTransport>();
            return transport[0];
        }

        internal class ErroringTelemetryAgent : MockTelemetryAgent
        {
            private readonly int _responseCode;

            public ErroringTelemetryAgent(int responseCode, int port)
                : base(port)
            {
                _responseCode = responseCode;
            }

            protected override void HandleHttpRequest(HttpListenerContext ctx)
            {
                OnRequestReceived(ctx);

                // make sure it works correctly
                var apiVersion = ctx.Request.Headers[TelemetryConstants.ApiVersionHeader];
                var requestType = ctx.Request.Headers[TelemetryConstants.RequestTypeHeader];

                var telemetry = DeserializeResponse(ctx.Request.InputStream, apiVersion, requestType);

                ctx.Response.StatusCode = _responseCode;
                ctx.Response.Close();
            }
        }

        internal class Data
        {
            public static IEnumerable<object[]> GetStatusCodes()
            {
                var pairs = new Dictionary<int, int>()
                {
                    { 200, (int)TelemetryPushResult.Success },
                    { 201, (int)TelemetryPushResult.Success },
                    { 400, (int)TelemetryPushResult.TransientFailure },
                    { 404, (int)TelemetryPushResult.FatalError },
                    { 500, (int)TelemetryPushResult.TransientFailure },
                    { 503, (int)TelemetryPushResult.TransientFailure },
                };

                foreach (var kvp in pairs)
                {
                    yield return new object[] { kvp.Key, kvp.Value, true };
                    yield return new object[] { kvp.Key, kvp.Value, false };
                }
            }
        }
    }
}
