// <copyright file="OtlpHeaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Logs;
using Datadog.Trace.OpenTelemetry.Metrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using LogsExporter = Datadog.Trace.OpenTelemetry.Logs.OtlpExporter;
using MetricsExporter = Datadog.Trace.OpenTelemetry.Metrics.OtlpExporter;

namespace Datadog.Trace.Tests.OpenTelemetry;

public class OtlpHeaderTests(ITestOutputHelper output)
{
    private const string EncodedHeaders = "api-key=hello%20world%2Cvalue%3D1,x-plain=plain+value,x-plus=a+b%2Bc,x%2Dkey=value,x-percent=100%25,x-once=%2520,x-lower=%2c%3d,x-malformed=bad%2G%,x-other=unchanged";

    private static readonly Dictionary<string, string> ExpectedHeaders = new()
    {
        ["api-key"] = "hello world,value=1",
        ["x-plain"] = "plain+value",
        ["x-plus"] = "a+b+c",
        ["x%2Dkey"] = "value",
        ["x-percent"] = "100%",
        ["x-once"] = "%20",
        ["x-lower"] = ",=",
        ["x-malformed"] = "bad%2G%",
        ["x-other"] = "unchanged",
    };

    [Theory]
    [CombinatorialData]
    public void SettingsDecodeHeaderValues(
        [CombinatorialValues("traces", "metrics", "logs")] string signal,
        bool signalSpecific)
    {
        var source = CreateSource(signal, signalSpecific, EncodedHeaders);
        var telemetry = new ConfigurationTelemetry();
        var settings = new TracerSettings(source, telemetry, new());
        var exporter = new ExporterSettings(source, _ => false, telemetry);
        var headers = GetHeaders(signal, settings, exporter);

        headers.Should().BeEquivalentTo(ExpectedHeaders);
        telemetry.GetQueueForTesting().Should().NotContain(entry => entry.StringValue != null && (entry.StringValue.Contains("hello") || entry.StringValue.Contains("bad%2G")));
    }

    [Theory]
    [CombinatorialData]
    public void PlainOrUnsetHeadersAreUnchanged(
        [CombinatorialValues("traces", "metrics", "logs")] string signal,
        bool signalSpecific,
        [CombinatorialValues(null, "", "api-key=plain+value")] string value)
    {
        var source = CreateSource(signal, signalSpecific, value);
        var settings = new TracerSettings(source);
        var exporter = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);
        var headers = GetHeaders(signal, settings, exporter);
        var expected = new Dictionary<string, string>();
        if (value is not null && value.Length > 0)
        {
            expected["api-key"] = "plain+value";
        }

        headers.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [CombinatorialData]
    public void SignalHeadersReplaceGenericHeaders(
        [CombinatorialValues("traces", "metrics", "logs")] string signal)
    {
        var source = new NameValueConfigurationSource(new NameValueCollection
        {
            ["OTEL_EXPORTER_OTLP_HEADERS"] = "api-key=generic%20value,x-generic=generic",
            [$"OTEL_EXPORTER_OTLP_{signal.ToUpperInvariant()}_HEADERS"] = "api-key=signal%20value",
        });
        var settings = new TracerSettings(source);
        var exporter = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);
        var headers = GetHeaders(signal, settings, exporter);

        headers.Should().BeEquivalentTo(new Dictionary<string, string> { ["api-key"] = "signal value" });
    }

    [Theory]
    [CombinatorialData]
    public async Task ExportSendsDecodedHeaders(
        [CombinatorialValues("traces", "metrics", "logs")] string signal,
        bool signalSpecific)
    {
        using var agent = MockTracerAgent.Create(output);
        agent.ShouldDeserializeTraces = false;
        var received = new TaskCompletionSource<MockHttpRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.RequestReceived += (_, args) =>
        {
            if (args.Value.PathAndQuery == $"/v1/{signal}")
            {
                received.TrySetResult(args.Value);
            }
        };
        var source = CreateSource(signal, signalSpecific, EncodedHeaders, $"http://127.0.0.1:{agent.Port}/v1/{signal}");
        var settings = new TracerSettings(source);
        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        if (signal == "traces")
        {
            var factory = OtlpTransportStrategy.GetTraces(exporterSettings);
            using var response = await factory.Create(factory.GetEndpoint(null)).PostAsync(new ArraySegment<byte>([]), "application/x-protobuf");
            response.StatusCode.Should().Be(200);
        }
        else if (signal == "metrics")
        {
            var exporter = new MetricsExporter(settings, exporterSettings);
            try
            {
                var point = new MetricPoint("header-test", "test", "1.0", [], InstrumentType.Gauge, null, new());
                (await exporter.ExportAsync([point])).Should().Be(ExportResult.Success);
            }
            finally
            {
                exporter.Shutdown();
            }
        }
        else
        {
            var exporter = new LogsExporter(settings, exporterSettings);
            try
            {
                (await exporter.ExportAsync([new LogPoint { Message = "header-test", LogLevel = 2, CategoryName = "test" }])).Should().Be(ExportResult.Success);
            }
            finally
            {
                exporter.Shutdown();
            }
        }

        var request = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var header in ExpectedHeaders)
        {
            var actual = request.Headers.GetValues(header.Key).Single();
            output.WriteLine($"{signal}/{(signalSpecific ? "signal" : "generic")}: {header.Key}={actual}");
            actual.Should().Be(header.Value);
        }
    }

    private static IReadOnlyDictionary<string, string> GetHeaders(string signal, TracerSettings settings, ExporterSettings exporter)
    {
        return signal switch
        {
            "traces" => exporter.OtlpTracesHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            "metrics" => exporter.OtlpMetricsHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            _ => settings.OtlpLogsHeaders,
        };
    }

    private static NameValueConfigurationSource CreateSource(string signal, bool signalSpecific, string headers, string endpoint = "http://localhost:4318")
    {
        return new NameValueConfigurationSource(new NameValueCollection
        {
            [signalSpecific ? $"OTEL_EXPORTER_OTLP_{signal.ToUpperInvariant()}_HEADERS" : "OTEL_EXPORTER_OTLP_HEADERS"] = headers,
            [$"OTEL_EXPORTER_OTLP_{signal.ToUpperInvariant()}_ENDPOINT"] = endpoint,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
            ["OTEL_EXPORTER_OTLP_TIMEOUT"] = "5000",
        });
    }
}

#endif
