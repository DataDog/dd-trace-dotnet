// <copyright file="JsonTelemetryTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Transports
{
    public class JsonTelemetryTransportTests
    {
#if !NETFRAMEWORK
        [Theory]
        [CombinatorialData]
        public async Task ShouldContainRequiredHeaders(bool debugEnabled, [CombinatorialValues("", "gzip")] string compression, bool agentless)
        {
            var containerMetadata = new ContainerMetadata("my-container-id", "my-entity-id");

            // set up the response returned by the request
            var fakeResponse = new TestApiResponse(statusCode: 200, body: null, contentType: null);

            // set up the request returned by the factory
            var savedHeaders = new Dictionary<string, string>();
            var requestMock = new Mock<IApiRequest>();
            requestMock.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(fakeResponse);
            requestMock.Setup(x => x.AddHeader(It.IsAny<string>(), It.IsAny<string>())).Callback((string k, string v) => savedHeaders.Add(k, v));

            // set up the factory passed to the transport
            var requestFactoryMock = new Mock<IApiRequestFactory>();
            requestFactoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(requestMock.Object);

            ITelemetryTransport telemetryTransport;
            // this actually doesn't change anything, but better test both
            if (agentless)
            {
                telemetryTransport = new AgentlessTelemetryTransport(requestFactoryMock.Object, debugEnabled, compression, containerMetadata);
            }
            else
            {
                telemetryTransport = new AgentTelemetryTransport(requestFactoryMock.Object, debugEnabled, compression, containerMetadata);
            }

            var data = new TelemetryData(
                "my-request-type",
                tracerTime: 0,
                string.Empty,
                seqId: 0,
                new ApplicationTelemetryData(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                new HostTelemetryData(string.Empty, string.Empty, string.Empty),
                payload: null);

            // the actual method being tested 👇
            var result = await telemetryTransport.PushTelemetry(data);

            result.Should().Be(TelemetryPushResult.Success);

            var allExpected = new Dictionary<string, string>
            {
                { "DD-Telemetry-API-Version", TelemetryConstants.ApiVersionV2 },
                { "DD-Telemetry-Request-Type", "my-request-type" },
                { "Datadog-Container-ID", "my-container-id" },
                { "Datadog-Entity-ID", "my-entity-id" }
            };
            if (debugEnabled)
            {
                allExpected["DD-Telemetry-Debug-Enabled"] = "true";
            }

            savedHeaders.Should().BeEquivalentTo(allExpected);

            var expectedEncoding = compression == "gzip" ? "gzip" : null;
            requestMock.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", expectedEncoding), Times.Once);
        }

#endif

        [Fact]
        public void SerializedAppStartedShouldProduceJsonWithExpectedFormat()
        {
            var expectedJson = JToken.Parse(GetAppStartedData());

            var data = new TelemetryData(
                requestType: "app-started",
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryData(
                    serviceName: "myapp",
                    env: "prod",
                    serviceVersion: "1.2.3",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3",
                    commitSha: "testCommitSha",
                    repositoryUrl: "testRepositoryUrl",
                    processTags: "entrypoint.basedir:Users,entrypoint.workdir:Downloads"),
                host: new HostTelemetryData(
                    hostname: "i-09ecf74c319c49be8",
                    os: "GNU/Linux",
                    architecture: "x86_64")
                {
                    OsVersion = "ubuntu 18.04.5 LTS (Bionic Beaver)",
                    KernelName = "Linux",
                    KernelRelease = "5.4.0-1037-gcp",
                    KernelVersion = "#40~18.04.1-Ubuntu SMP Fri Feb 5 15:41:35 UTC 2021"
                },
                payload: new AppStartedPayload()
                {
                    Configuration = new List<ConfigurationKeyValue>
                    {
                        new("DD_TRACE_AGENT_URL", "http://localhost:9126", "env_var", 1, null),
                        new("DD_TRACE_DEBUG", "true", "env_var", 2, null),
                        new("DD_TRACE_ENABLED", true, "env_var", 3, null),
                        new("DD_API_KEY", "<redacted>", "env_var", 4, TelemetryErrorCode.FailedValidation),
                    },
                    Products = new ProductsData
                    {
                        Appsec = new ProductData(false, null),
                        Profiler = new ProductData(true, new ErrorData((TelemetryErrorCode)1, "Some error"))
                    },
                    InstallSignature = new AppStartedPayload.InstallSignaturePayload
                    {
                        InstallId = "68e75c48-57ca-4a12-adfc-575c4b05fcbe",
                        InstallTime = "1703188212",
                        InstallType = "k8s_single_step"
                    },
                })
            {
                NamingSchemaVersion = "1"
            };

            var serialized = JsonTelemetryTransport.SerializeTelemetry(data);
            serialized.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(serialized);

            actualJson.Should().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public void SerializedMetricsTelemetryShouldProduceJsonWithExpectedFormat()
        {
            var expectedJson = JToken.Parse(GetGenerateMetricsData());

            var data = new TelemetryData(
                requestType: TelemetryRequestTypes.GenerateMetrics,
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryData(
                    serviceName: "myapp",
                    env: "prod",
                    serviceVersion: "1.2.3",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3",
                    commitSha: "testCommitSha",
                    repositoryUrl: "testRepositoryUrl",
                    processTags: "entrypoint.basedir:Users,entrypoint.workdir:Downloads"),
                host: new HostTelemetryData(
                    hostname: "i-09ecf74c319c49be8",
                    os: "GNU/Linux",
                    architecture: "x86_64")
                {
                    OsVersion = "ubuntu 18.04.5 LTS (Bionic Beaver)",
                    KernelName = "Linux",
                    KernelRelease = "5.4.0-1037-gcp",
                    KernelVersion = "#40~18.04.1-Ubuntu SMP Fri Feb 5 15:41:35 UTC 2021"
                },
                payload: new GenerateMetricsPayload(new MetricData[]
                    {
                        new(
                            "tracer_init_time",
                            new MetricSeries()
                        {
                            new(1575317847, 2241),
                            new(1575317947, 2352),
                        },
                            common: true,
                            type: MetricTypeConstants.Count)
                        {
                            Tags = new[]
                            {
                                "org_id: 2",
                                "environment:test"
                            }
                        },
                        new(
                            "app_sec_initialization_time",
                            new MetricSeries()
                            {
                                new(1575317447, 254),
                                new(1575317547, 643),
                            },
                            common: false,
                            type: MetricTypeConstants.Gauge)
                        {
                            Namespace = MetricNamespaceConstants.ASM,
                            Interval = 60,
                        },
                    }));

            var serialized = JsonTelemetryTransport.SerializeTelemetry(data);
            serialized.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(serialized);

            actualJson.Should().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public void SerializedDistributionMetricsTelemetryShouldProduceJsonWithExpectedFormat()
        {
            var expectedJson = JToken.Parse(GetDistributionsData());

            var data = new TelemetryData(
                requestType: TelemetryRequestTypes.Distributions,
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryData(
                    serviceName: "myapp",
                    env: "prod",
                    serviceVersion: "1.2.3",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3",
                    commitSha: "testCommitSha",
                    repositoryUrl: "testRepositoryUrl",
                    processTags: "entrypoint.basedir:Users,entrypoint.workdir:Downloads"),
                host: new HostTelemetryData(
                    hostname: "i-09ecf74c319c49be8",
                    os: "GNU/Linux",
                    architecture: "x86_64")
                {
                    OsVersion = "ubuntu 18.04.5 LTS (Bionic Beaver)",
                    KernelName = "Linux",
                    KernelRelease = "5.4.0-1037-gcp",
                    KernelVersion = "#40~18.04.1-Ubuntu SMP Fri Feb 5 15:41:35 UTC 2021"
                },
                payload: new DistributionsPayload(
                    series: new DistributionMetricData[]
                    {
                        new(
                            "init_time",
                            new() { 224.1 },
                            common: true)
                        {
                            Tags = new[]
                            {
                                "org_id: 2",
                                "component:total"
                            }
                        },
                        new(
                            "app_sec_init_time",
                            new() { 424.2, 232 },
                            common: false)
                        {
                            Namespace = MetricNamespaceConstants.ASM,
                            Tags = new[]
                            {
                                "org_id: 2",
                                "component:native_lib"
                            }
                        },
                    }));

            var serialized = JsonTelemetryTransport.SerializeTelemetry(data);
            serialized.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(serialized);

            actualJson.Should().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public void SerializedMessageBatchShouldProduceJsonWithExpectedFormat()
        {
            var expectedJson = JToken.Parse(GetMessageBatchData());

            var data = new TelemetryData(
                requestType: TelemetryRequestTypes.MessageBatch,
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryData(
                    serviceName: "myapp",
                    env: "prod",
                    serviceVersion: "1.2.3",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3",
                    commitSha: "testCommitSha",
                    repositoryUrl: "testRepositoryUrl",
                    processTags: "entrypoint.basedir:Users,entrypoint.workdir:Downloads"),
                host: new HostTelemetryData(
                    hostname: "i-09ecf74c319c49be8",
                    os: "GNU/Linux",
                    architecture: "x86_64")
                {
                    OsVersion = "ubuntu 18.04.5 LTS (Bionic Beaver)",
                    KernelName = "Linux",
                    KernelRelease = "5.4.0-1037-gcp",
                    KernelVersion = "#40~18.04.1-Ubuntu SMP Fri Feb 5 15:41:35 UTC 2021"
                },
                payload: new MessageBatchPayload(
                    new List<MessageBatchData>
                    {
                        new(
                            TelemetryRequestTypes.AppIntegrationsChanged,
                            new AppIntegrationsChangedPayload(new List<IntegrationTelemetryData>
                            {
                                new(name: "express", enabled: true, autoEnabled: true, error: null),
                                new(name: "pg", enabled: false, autoEnabled: false, error: "there was an error"),
                            })),
                        new(
                            TelemetryRequestTypes.AppDependenciesLoaded,
                            new AppDependenciesLoadedPayload(new List<DependencyTelemetryData>
                            {
                                new(name: "pg") { Version = "8.6.0" },
                                new(name: "express") { Version = "4.17.1" },
                                new(name: "body-parser") { Version = "1.19.0", Hash = "646DF3C3-959F-4011-8673-EE58BD9291E2" },
                            })),
                        new(
                            TelemetryRequestTypes.AppClientConfigurationChanged,
                            new AppClientConfigurationChangedPayload(new List<ConfigurationKeyValue>
                            {
                                new("DD_TRACE_AGENT_URL", "http://localhost:9126", "env_var", 1, null),
                                new("DD_TRACE_DEBUG", "true", "env_var", 2, null),
                                new("DD_TRACE_ENABLED", true, "env_var", 3, null),
                                new("DD_API_KEY", "<redacted>", "env_var", 4, TelemetryErrorCode.FailedValidation),
                            })),
                    }))
            {
                NamingSchemaVersion = "1"
            };

            var serialized = JsonTelemetryTransport.SerializeTelemetry(data);
            serialized.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(serialized);

            actualJson.Should().BeEquivalentTo(expectedJson);
        }

        private static string GetAppStartedData()
            => GetSampleTelemetryData("telemetry_app-started.json");

        private static string GetMessageBatchData()
            => GetSampleTelemetryData("telemetry_message-batch.json");

        private static string GetGenerateMetricsData()
            => GetSampleTelemetryData("telemetry_generate-metrics.json");

        private static string GetDistributionsData()
            => GetSampleTelemetryData("telemetry_distributions.json");

        private static string GetSampleTelemetryData(string filename)
        {
            var thisAssembly = typeof(JsonTelemetryTransportTests).Assembly;
            var stream = thisAssembly.GetManifestResourceStream($"Datadog.Trace.Tests.Telemetry.{filename}");
            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }
    }
}
