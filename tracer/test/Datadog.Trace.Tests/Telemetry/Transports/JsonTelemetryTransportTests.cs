// <copyright file="JsonTelemetryTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class JsonTelemetryTransportTests
    {
        [Fact]
        public async Task SerializeTelemetryShouldProduceJsonWithExpectedFormat()
        {
            var transport = new TestJsonTelemetryTransport();
            var expectedJson = JToken.Parse(GetSampleTelemetryData());

            var data = new TelemetryData
            {
                RequestType = "app-started",
                RuntimeId = "20338dfd-f700-4e5c-b3f6-0d470f054ae8",
                SeqId = 5672,
                TracerTime = 1628099086,
                Application = new ApplicationTelemetryData
                {
                    ServiceName = "myapp",
                    Env = "prod",
                    ServiceVersion = "1.2.3",
                    TracerVersion = "0.33.1",
                    LanguageName = "node.js",
                    LanguageVersion = "14.16.1",
                },
                Payload = new AppStartedPayload
                {
                    Integrations = new List<IntegrationTelemetryData>
                    {
                        new()
                        {
                            Name = "express",
                            Enabled = true,
                            AutoEnabled = true
                        },
                        new()
                        {
                            Name = "pg",
                            Enabled = false,
                            AutoEnabled = false,
                            Compatible = false
                        }
                    },
                    Dependencies = new List<DependencyTelemetryData>
                    {
                        new()
                        {
                            Name = "pg",
                            Version = "8.6.0"
                        },
                        new()
                        {
                            Name = "express",
                            Version = "4.17.1"
                        },
                        new()
                        {
                            Name = "body-parser",
                            Version = "1.19.0"
                        },
                    },
                    Configuration = new ConfigTelemetryData { OsVersion = "10", OsName = "Windows" },
                    AdditionalPayload = new Dictionary<string, object> { { "to_be", "determined" }, }
                }
            };

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

        internal class TestJsonTelemetryTransport : JsonTelemetryTransportBase
        {
            public string SerializedData { get; private set; }

            public override Task PushTelemetry(TelemetryData data)
            {
                SerializedData = SerializeTelemetry(data);
                return Task.FromResult(0);
            }
        }
    }
}
