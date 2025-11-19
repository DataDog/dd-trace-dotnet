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
        private const string GzipCompression = "gzip";
        private const string NoCompression = "none";
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);

        [Fact]
        public async Task CanSendTelemetry()
        {
            using var agent = new MockTelemetryAgent(TcpPortProvider.GetOpenPort());
            var telemetryUri = new Uri($"http://localhost:{agent.Port}");

            // Uses framework specific transport
            var transport = GetAgentOnlyTransport(telemetryUri, GzipCompression);
            var data =  GetSampleData();
            var result = await transport.PushTelemetry(data);

            result.Should().Be(TelemetryPushResult.Success);
            var received = await agent.WaitForLatestTelemetryAsync(x => x.SeqId == data.SeqId);

            received.Should().NotBeNull();

            // check some basic values
            received.SeqId.Should().Be(data.SeqId);
            var v2 = received.Should().BeOfType<TelemetryData>().Subject;
            v2.Application.Env.Should().Be(data.Application.Env);
            v2.Application.ServiceName.Should().Be(data.Application.ServiceName);
        }

        [SkippableTheory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task SetsRequiredHeaders(bool agentless, bool useCloudAgentless)
        {
            const string apiKey = "some key";
            using var agent = new MockTelemetryAgent(TcpPortProvider.GetOpenPort());
            var telemetryUri = new Uri($"http://localhost:{agent.Port}");

            var cloud = useCloudAgentless
                            ? new TelemetrySettings.AgentlessSettings.CloudSettings("Provider", "Resource", "ID")
                            : null;

            // Uses framework specific transport
            var transport = agentless
                                ? GetAgentlessOnlyTransport(telemetryUri, apiKey, cloud)
                                : GetAgentOnlyTransport(telemetryUri, GzipCompression);

            var data = GetSampleData();
            var result = await transport.PushTelemetry(data);

            result.Should().Be(TelemetryPushResult.Success);
            var received = await agent.WaitForLatestTelemetryAsync(x => x.SeqId == data.SeqId);

            received.Should().NotBeNull();

            var allExpected = new Dictionary<string, string>
            {
                { "DD-Telemetry-API-Version", TelemetryConstants.ApiVersionV2 },
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

            if (ContainerMetadata.GetEntityId() is { } entityId)
            {
                allExpected.Add("Datadog-Entity-ID", entityId);
            }

            if (agentless)
            {
                allExpected.Add(TelemetryConstants.ApiKeyHeader, apiKey);
            }

            if (useCloudAgentless)
            {
                allExpected.Add(TelemetryConstants.CloudProviderHeader, cloud.Provider);
                allExpected.Add(TelemetryConstants.CloudResourceTypeHeader, cloud.ResourceType);
                allExpected.Add(TelemetryConstants.CloudResourceIdentifierHeader, cloud.ResourceIdentifier);
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

        [Fact]
        public async Task WhenNoListener_ReturnsFatalError()
        {
            // Nothing listening on this port (currently)
            var port = TcpPortProvider.GetOpenPort();
            var telemetryUri = new Uri($"http://localhost:{port}");

            // Uses framework specific transport
            var transport = GetAgentOnlyTransport(telemetryUri, GzipCompression);
            var data = GetSampleData();
            var result = await transport.PushTelemetry(data);

            result.Should().Be(TelemetryPushResult.FatalError);
        }

        [SkippableTheory]
        [MemberData(nameof(Data.GetStatusCodes), MemberType = typeof(Data))]
        public async Task ReturnsExpectedPushResultForStatusCode(int responseCode, string compressionEnabled, int expectedPushResult)
        {
            using var agent = new ErroringTelemetryAgent(
                responseCode: responseCode,
                port: TcpPortProvider.GetOpenPort());
            var telemetryUri = new Uri($"http://localhost:{agent.Port}");
            var transport = GetAgentOnlyTransport(telemetryUri, compressionEnabled);
            var data = GetSampleData();
            var result = await transport.PushTelemetry(data);

            result.Should().Be((TelemetryPushResult)expectedPushResult);
        }

        private static TelemetryData GetSampleData() =>
            new(
                requestType: TelemetryRequestTypes.AppHeartbeat,
                tracerTime: 1234,
                runtimeId: "some-value",
                seqId: 23,
                application: new ApplicationTelemetryData(
                    serviceName: "TelemetryTransportTests",
                    env: "TracerTelemetryTest",
                    serviceVersion: "123",
                    tracerVersion: TracerConstants.AssemblyVersion,
                    languageName: "dotnet",
                    languageVersion: "1.2.3",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3",
                    commitSha: "aaaaaaaaaaaaaaaaaa",
                    repositoryUrl: "https://github.com/myOrg/myRepo",
                    processTags: "entrypoint.basedir:Users,entrypoint.workdir:Downloads"),
                host: new HostTelemetryData("SOME_HOST", "Windows", "x64"),
                payload: null);

        private static ITelemetryTransport GetAgentOnlyTransport(Uri telemetryUri, string compressionMethod)
        {
            var transport = TelemetryTransportFactory.Create(
                new TelemetrySettings(telemetryEnabled: true, configurationError: null, agentlessSettings: null, agentProxyEnabled: true, heartbeatInterval: HeartbeatInterval, dependencyCollectionEnabled: true, metricsEnabled: false, debugEnabled: false, compressionMethod: compressionMethod),
                ExporterSettings.Create(new() { { ConfigurationKeys.AgentUri, telemetryUri } }));
            transport.AgentTransport.Should().NotBeNull().And.BeOfType<AgentTelemetryTransport>();
            return transport.AgentTransport;
        }

        private static ITelemetryTransport GetAgentlessOnlyTransport(Uri telemetryUri, string apiKey, TelemetrySettings.AgentlessSettings.CloudSettings cloudSettings)
        {
            var agentlessSettings = new TelemetrySettings.AgentlessSettings(telemetryUri, apiKey, cloudSettings);

            var transport = TelemetryTransportFactory.Create(
                new TelemetrySettings(telemetryEnabled: true, configurationError: null, agentlessSettings, agentProxyEnabled: false, heartbeatInterval: HeartbeatInterval, dependencyCollectionEnabled: true, metricsEnabled: false, debugEnabled: false, compressionMethod: GzipCompression),
                new ExporterSettings());

            transport.AgentlessTransport.Should().NotBeNull().And.BeOfType<AgentlessTelemetryTransport>();
            return transport.AgentlessTransport;
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
                // make sure it works correctly
                var apiVersion = ctx.Request.Headers[TelemetryConstants.ApiVersionHeader];
                var requestType = ctx.Request.Headers[TelemetryConstants.RequestTypeHeader];
                var compressed = string.Equals(ctx.Request.Headers["Content-Encoding"], GzipCompression, StringComparison.OrdinalIgnoreCase);

                var telemetry = DeserializeResponse(ctx.Request.InputStream, apiVersion, requestType, compressed);

                ctx.Response.StatusCode = _responseCode;
                ctx.Response.Close();
            }
        }

        internal class Data
        {
            public static TheoryData<int, string, int> GetStatusCodes() => new()
            {
                { 200, GzipCompression, (int)TelemetryPushResult.Success },
                { 201, GzipCompression, (int)TelemetryPushResult.Success },
                { 400, GzipCompression, (int)TelemetryPushResult.TransientFailure },
                { 404, GzipCompression, (int)TelemetryPushResult.FatalError },
                { 500, GzipCompression, (int)TelemetryPushResult.TransientFailure },
                { 503, GzipCompression, (int)TelemetryPushResult.TransientFailure },
                { 200, NoCompression, (int)TelemetryPushResult.Success },
                { 201, NoCompression, (int)TelemetryPushResult.Success },
                { 400, NoCompression, (int)TelemetryPushResult.TransientFailure },
                { 404, NoCompression, (int)TelemetryPushResult.FatalError },
                { 500, NoCompression, (int)TelemetryPushResult.TransientFailure },
                { 503, NoCompression, (int)TelemetryPushResult.TransientFailure },
            };
        }
    }
}
