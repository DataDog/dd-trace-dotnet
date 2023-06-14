// <copyright file="JsonTelemetryTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Tests.Util.JsonAssertions;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Transports
{
    public class JsonTelemetryTransportTests
    {
        [Fact]
        public void SerializedAppStartedShouldProduceJsonWithExpectedFormat()
        {
            var expectedJson = JToken.Parse(GetAppStartedDataV1());

            var data = new TelemetryData(
                requestType: "app-started",
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryData(
                    serviceName: "myapp",
                    env: "prod",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1")
                {
                    ServiceVersion = "1.2.3",
                },
                host: new HostTelemetryData
                {
                    Hostname = "i-09ecf74c319c49be8",
                    ContainerId = "d39b145254d1f9c337fdd2be132f6650c6f5bc274bfa28aaa204a908a1134096",
                    Os = "GNU/Linux",
                    OsVersion = "ubuntu 18.04.5 LTS (Bionic Beaver)",
                    KernelName = "Linux",
                    KernelRelease = "5.4.0-1037-gcp",
                    KernelVersion = "#40~18.04.1-Ubuntu SMP Fri Feb 5 15:41:35 UTC 2021"
                },
                payload: new AppStartedPayload(
                    integrations: new List<IntegrationTelemetryData>
                    {
                        new(name: "express", enabled: true, autoEnabled: true, error: null),
                        new(name: "pg", enabled: false, autoEnabled: false, error: "there was an error"),
                    },
                    dependencies: new List<DependencyTelemetryData>
                    {
                        new(name: "pg") { Version = "8.6.0" },
                        new(name: "express") { Version = "4.17.1" },
                        new(name: "body-parser") { Version = "1.19.0", Hash = "646DF3C3-959F-4011-8673-EE58BD9291E2" },
                    },
                    configuration: new List<TelemetryValue>())
                    {
                        AdditionalPayload = new List<TelemetryValue> { new(name: "to_be", value: "determined") }
                    });

            var serialized = JsonTelemetryTransport.SerializeTelemetry(data);
            serialized.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(serialized);

            actualJson.JsonShould().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public void SerializedAppStartedShouldProduceJsonWithExpectedFormatV2()
        {
            var expectedJson = JToken.Parse(GetAppStartedDataV2());

            var data = new TelemetryDataV2(
                requestType: "app-started",
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryDataV2(
                    serviceName: "myapp",
                    env: "prod",
                    serviceVersion: "1.2.3",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3"),
                host: new HostTelemetryDataV2(
                    hostname: "i-09ecf74c319c49be8",
                    os: "GNU/Linux",
                    architecture: "x86_64")
                {
                    OsVersion = "ubuntu 18.04.5 LTS (Bionic Beaver)",
                    KernelName = "Linux",
                    KernelRelease = "5.4.0-1037-gcp",
                    KernelVersion = "#40~18.04.1-Ubuntu SMP Fri Feb 5 15:41:35 UTC 2021"
                },
                payload: new AppStartedPayloadV2()
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
                    }
                })
            {
                NamingSchemaVersion = 1
            };

            var serialized = JsonTelemetryTransport.SerializeTelemetry(data);
            serialized.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(serialized);

            actualJson.JsonShould().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public void SerializedMetricsTelemetryShouldProduceJsonWithExpectedFormat()
        {
            var expectedJson = JToken.Parse(GetGenerateMetricsData());

            var data = new TelemetryDataV2(
                requestType: TelemetryRequestTypes.GenerateMetrics,
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryDataV2(
                    serviceName: "myapp",
                    env: "prod",
                    serviceVersion: "1.2.3",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3"),
                host: new HostTelemetryDataV2(
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
                            new(1575317847, 224.1),
                            new(1575317947, 235.2),
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
                                new(1575317447, 2.54),
                                new(1575317547, 6.43),
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

            actualJson.JsonShould().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public void SerializedDistributionMetricsTelemetryShouldProduceJsonWithExpectedFormat()
        {
            var expectedJson = JToken.Parse(GetDistributionsData());

            var data = new TelemetryDataV2(
                requestType: TelemetryRequestTypes.Distributions,
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryDataV2(
                    serviceName: "myapp",
                    env: "prod",
                    serviceVersion: "1.2.3",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3"),
                host: new HostTelemetryDataV2(
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

            actualJson.JsonShould().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public void SerializedMessageBatchShouldProduceJsonWithExpectedFormatV2()
        {
            var expectedJson = JToken.Parse(GetMessageBatchDataV2());

            var data = new TelemetryDataV2(
                requestType: TelemetryRequestTypes.MessageBatch,
                runtimeId: "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                seqId: 5672,
                tracerTime: 1628099086,
                application: new ApplicationTelemetryDataV2(
                    serviceName: "myapp",
                    env: "prod",
                    serviceVersion: "1.2.3",
                    tracerVersion: "0.33.1",
                    languageName: "node.js",
                    languageVersion: "14.16.1",
                    runtimeName: "dotnet",
                    runtimeVersion: "7.0.3"),
                host: new HostTelemetryDataV2(
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
                            new AppClientConfigurationChangedPayloadV2(new List<ConfigurationKeyValue>
                            {
                                new("DD_TRACE_AGENT_URL", "http://localhost:9126", "env_var", 1, null),
                                new("DD_TRACE_DEBUG", "true", "env_var", 2, null),
                                new("DD_TRACE_ENABLED", true, "env_var", 3, null),
                                new("DD_API_KEY", "<redacted>", "env_var", 4, TelemetryErrorCode.FailedValidation),
                            })),
                    }))
            {
                NamingSchemaVersion = 1
            };

            var serialized = JsonTelemetryTransport.SerializeTelemetry(data);
            serialized.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(serialized);

            actualJson.JsonShould().BeEquivalentTo(expectedJson);
        }

        private static string GetAppStartedDataV1()
            => GetSampleTelemetryData("telemetry_app-started-v1.json");

        private static string GetAppStartedDataV2()
            => GetSampleTelemetryData("telemetry_app-started-v2.json");

        private static string GetMessageBatchDataV2()
            => GetSampleTelemetryData("telemetry_message-batch-v2.json");

        private static string GetGenerateMetricsData()
            => GetSampleTelemetryData("telemetry_generate-metrics-v2.json");

        private static string GetDistributionsData()
            => GetSampleTelemetryData("telemetry_distributions-v2.json");

        private static string GetSampleTelemetryData(string filename)
        {
            var thisAssembly = typeof(JsonTelemetryTransportTests).Assembly;
            var stream = thisAssembly.GetManifestResourceStream($"Datadog.Trace.Tests.Telemetry.{filename}");
            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }
    }
}
