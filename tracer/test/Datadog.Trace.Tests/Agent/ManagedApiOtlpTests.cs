// <copyright file="ManagedApiOtlpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Specialized;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class ManagedApiOtlpTests
{
    [Fact]
    public void CreateOtlpRequestFactory_WhenApmUsesUnixDomainSocket_UsesHttpForOtlp()
    {
        // Simulate the Linux scenario: APM UDS exists at the default path,
        // and the OTLP endpoint defaults to http://localhost:4318
        var source = BuildSource("OTEL_EXPORTER_OTLP_ENDPOINT:http://localhost:4318");
        Func<string, bool> fileExists = path => path == ExporterSettings.DefaultTracesUnixDomainSocket;
        var exporterSettings = new ExporterSettings(source, fileExists, NullConfigurationTelemetry.Instance);

        // Verify the APM transport is UDS (this is the precondition that caused the bug)
        exporterSettings.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);

        var factory = ManagedApiOtlp.CreateOtlpRequestFactory(exporterSettings);

        // The factory must be a plain HttpClientRequestFactory, not a
        // SocketHandlerRequestFactory (which would route through the APM UDS)
        factory.Should().BeOfType<HttpClientRequestFactory>();
    }

    [Theory]
    [InlineData("http://localhost:4318", "http://localhost:4318/v1/traces")]
    [InlineData("http://otel-collector:4318", "http://otel-collector:4318/v1/traces")]
    [InlineData("http://localhost:9999", "http://localhost:9999/v1/traces")]
    public void CreateOtlpRequestFactory_UsesCorrectOtlpEndpoint(string otlpEndpoint, string expectedTracesEndpoint)
    {
        var source = BuildSource($"OTEL_EXPORTER_OTLP_ENDPOINT:{otlpEndpoint}");
        Func<string, bool> fileExists = path => path == ExporterSettings.DefaultTracesUnixDomainSocket;
        var exporterSettings = new ExporterSettings(source, fileExists, NullConfigurationTelemetry.Instance);

        // Verify the OTLP endpoint is correct
        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri(expectedTracesEndpoint));

        var factory = ManagedApiOtlp.CreateOtlpRequestFactory(exporterSettings);

        // The factory should target the OTLP endpoint's authority
        var expectedBase = new Uri($"{new Uri(expectedTracesEndpoint).Scheme}://{new Uri(expectedTracesEndpoint).Authority}");
        factory.GetEndpoint("/v1/traces").Should().Be(new Uri(expectedBase, "/v1/traces"));
    }

    [Fact]
    public void CreateOtlpRequestFactory_WithExplicitTracesEndpoint_UsesSignalEndpoint()
    {
        var source = BuildSource(
            "OTEL_EXPORTER_OTLP_ENDPOINT:http://general-endpoint:4318",
            "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT:http://traces-endpoint:4318/v1/traces");
        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri("http://traces-endpoint:4318/v1/traces"));

        var factory = ManagedApiOtlp.CreateOtlpRequestFactory(exporterSettings);

        factory.Should().BeOfType<HttpClientRequestFactory>();
        factory.GetEndpoint("/v1/traces").Should().Be(new Uri("http://traces-endpoint:4318/v1/traces"));
    }

    private static NameValueConfigurationSource BuildSource(params string[] config)
    {
        var configNameValues = new NameValueCollection();

        foreach (var item in config)
        {
            var separatorIndex = item.IndexOf(':');
            configNameValues.Add(item.Substring(0, separatorIndex), item.Substring(separatorIndex + 1));
        }

        return new NameValueConfigurationSource(configNameValues);
    }
}

#endif
