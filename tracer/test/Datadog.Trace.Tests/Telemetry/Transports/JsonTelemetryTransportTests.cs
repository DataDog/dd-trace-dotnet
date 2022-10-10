// <copyright file="JsonTelemetryTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Transports
{
    public class JsonTelemetryTransportTests
    {
        [Fact]
        public async Task SerializeTelemetryShouldProduceJsonWithExpectedFormat()
        {
            var transport = new TestJsonTelemetryTransport();
            var expectedJson = JToken.Parse(GetSampleTelemetryData());

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
                        new(name: "express", enabled: true) { AutoEnabled = true },
                        new(name: "pg", enabled: false) { AutoEnabled = false, Compatible = false },
                    },
                    dependencies: new List<DependencyTelemetryData>
                    {
                        new(name: "express") { Version = "8.6.0" },
                        new(name: "express") { Version = "4.17.1" },
                        new(name: "body-parser") { Version = "1.19.0" },
                    },
                    configuration: new List<TelemetryValue>())
                    {
                        AdditionalPayload = new List<TelemetryValue> { new(name: "to_be", value: "determined") }
                    });

            await transport.PushTelemetry(data);
            transport.SerializedData.Should().NotBeNullOrEmpty();
            var actualJson = JToken.Parse(transport.SerializedData);

            actualJson.Should().BeEquivalentTo(expectedJson);
        }

        private static string GetSampleTelemetryData()
        {
            var thisAssembly = typeof(JsonTelemetryTransportTests).Assembly;
            var stream = thisAssembly.GetManifestResourceStream("Datadog.Trace.Tests.Telemetry.telemetry_data.json");
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
