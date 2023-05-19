// <copyright file="JsonTelemetryTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        public async Task SerializedAppStartedShouldProduceJsonWithExpectedFormat()
        {
            var transport = new TestJsonTelemetryTransport();
            var expectedJson = JToken.Parse(GetAppStartedData());

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

            await transport.PushTelemetry(data);
            transport.SerializedData.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(transport.SerializedData);

            actualJson.JsonShould().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public async Task SerializedMetricsTelemetryShouldProduceJsonWithExpectedFormat()
        {
            var transport = new TestJsonTelemetryTransport();
            var expectedJson = JToken.Parse(GetGenerateMetricsData());

            var data = new TelemetryData(
                requestType: TelemetryRequestTypes.GenerateMetrics,
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
                            Tags = new()
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

            await transport.PushTelemetry(data);
            transport.SerializedData.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(transport.SerializedData);

            actualJson.JsonShould().BeEquivalentTo(expectedJson);
        }

        [Fact]
        public async Task SerializedDistributionMetricsTelemetryShouldProduceJsonWithExpectedFormat()
        {
            var transport = new TestJsonTelemetryTransport();
            var expectedJson = JToken.Parse(GetDistributionsData());

            var data = new TelemetryData(
                requestType: TelemetryRequestTypes.Distributions,
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
                payload: new DistributionsPayload(
                    series: new DistributionMetricData[]
                    {
                        new(
                            "init_time",
                            new[] { 224.1 },
                            common: true)
                        {
                            Tags = new()
                            {
                                "org_id: 2",
                                "component:total"
                            }
                        },
                        new(
                            "app_sec_init_time",
                            new[] { 424.2, 232 },
                            common: false)
                        {
                            Namespace = MetricNamespaceConstants.ASM,
                            Tags = new()
                            {
                                "org_id: 2",
                                "component:native_lib"
                            }
                        },
                    }));

            await transport.PushTelemetry(data);
            transport.SerializedData.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(transport.SerializedData);

            actualJson.JsonShould().BeEquivalentTo(expectedJson);
        }

        private static string GetAppStartedData()
            => GetSampleTelemetryData("telemetry_app-started.json");

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

        internal class TestJsonTelemetryTransport : ITelemetryTransport
        {
            public string SerializedData { get; private set; }

            public Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
            {
                SerializedData = JsonTelemetryTransport.SerializeTelemetry(data);
                return Task.FromResult(TelemetryPushResult.Success);
            }

            public string GetTransportInfo() => nameof(TestJsonTelemetryTransport);
        }
    }
}
