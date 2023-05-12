﻿// <copyright file="TelemetryTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests
{
    public class TelemetryTransportTests
    {
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);

        [SkippableFact]
        public async Task CanSendTelemetry()
        {
            using var agent = new MockTelemetryAgent(TcpPortProvider.GetOpenPort());
            var telemetryUri = new Uri($"http://localhost:{agent.Port}");

            // Uses framework specific transport
            var transport = GetAgentOnlyTransport(telemetryUri);
            var data = GetSampleData();

            var result = await transport.PushTelemetry(data);

            result.Should().Be(TelemetryPushResult.Success);
            var received = agent.WaitForLatestTelemetry(x => x.SeqId == data.SeqId);

            received.Should().NotBeNull();

            // check some basic values
            received.SeqId.Should().Be(data.SeqId);
            received.Application.Env.Should().Be(data.Application.Env);
            received.Application.ServiceName.Should().Be(data.Application.ServiceName);
        }

        [SkippableFact]
        public async Task WhenNoListener_ReturnsFatalError()
        {
            // Nothing listening on this port (currently)
            var port = TcpPortProvider.GetOpenPort();
            var telemetryUri = new Uri($"http://localhost:{port}");

            // Uses framework specific transport
            var transport = GetAgentOnlyTransport(telemetryUri);
            var data = GetSampleData();

            var result = await transport.PushTelemetry(data);

            result.Should().Be(TelemetryPushResult.FatalError);
        }

        [SkippableTheory]
        [InlineData(200, (int)TelemetryPushResult.Success)]
        [InlineData(201, (int)TelemetryPushResult.Success)]
        [InlineData(400, (int)TelemetryPushResult.TransientFailure)]
        [InlineData(404, (int)TelemetryPushResult.FatalError)]
        [InlineData(500, (int)TelemetryPushResult.TransientFailure)]
        [InlineData(503, (int)TelemetryPushResult.TransientFailure)]
        public async Task ReturnsExpectedPushResultForStatusCode(int responseCode, int expectedPushResult)
        {
            using var agent = new ErroringTelemetryAgent(
                responseCode: responseCode,
                port: TcpPortProvider.GetOpenPort());
            var telemetryUri = new Uri($"http://localhost:{agent.Port}");
            var transport = GetAgentOnlyTransport(telemetryUri);
            var data = GetSampleData();

            var result = await transport.PushTelemetry(data);

            result.Should().Be((TelemetryPushResult)expectedPushResult);
        }

        private static TelemetryData GetSampleData()
        {
            return new TelemetryData(
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
                payload: null);
        }

        private static ITelemetryTransport GetAgentOnlyTransport(Uri telemetryUri)
        {
            var transport = TelemetryTransportFactory.Create(
                new TelemetrySettings(telemetryEnabled: true, configurationError: null, agentlessSettings: null, agentProxyEnabled: true, heartbeatInterval: HeartbeatInterval),
                new ImmutableExporterSettings(new ExporterSettings { AgentUri = telemetryUri }));
            transport.Should().HaveCount(1);
            transport[0].Should().BeOfType<AgentTelemetryTransport>();
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
    }
}
