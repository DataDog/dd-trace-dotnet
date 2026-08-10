// <copyright file="OtlpExporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Logs;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry.Logs;

public class OtlpExporterTests
{
    private const int MaxBufferSize = 3 * 1024 * 1024;

    [Fact]
    public void CreateHttpClient_SetsTracingDisabledHeader()
    {
        using var client = OtlpExporter.CreateHttpClient(timeoutMs: 5000, headers: new Dictionary<string, string>());

        client.DefaultRequestHeaders.TryGetValues(HttpHeaderNames.TracingEnabled, out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("false");
    }

    [Fact]
    public void CreateHttpClient_IncludesCustomHeaders()
    {
        var customHeaders = new Dictionary<string, string> { ["X-My-Header"] = "my-value" };

        using var client = OtlpExporter.CreateHttpClient(timeoutMs: 5000, headers: customHeaders);

        client.DefaultRequestHeaders.TryGetValues("X-My-Header", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("my-value");
    }

    [Fact]
    public async Task ExportAsync_PayloadExceedingMaximumSize_IsDroppedWithoutSending()
    {
        var source = new NameValueConfigurationSource(
            new NameValueCollection
            {
                [ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsEndpoint] = "http://127.0.0.1:1/v1/logs",
                [ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsProtocol] = "http/protobuf",
                [ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsTimeoutMs] = "100",
            });
        var handler = new MockHttpMessageHandler();
        var exporter = new OtlpExporter(new TracerSettings(source), new HttpClient(handler));
        var logs = new List<LogPoint>
        {
            new() { Message = new string('x', MaxBufferSize), LogLevel = 2, CategoryName = "Test" },
        };

        try
        {
            var result = await exporter.ExportAsync(logs);

            result.Should().Be(ExportResult.Success);
            handler.RequestCount.Should().Be(0);
        }
        finally
        {
            exporter.Shutdown();
        }
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

#endif
