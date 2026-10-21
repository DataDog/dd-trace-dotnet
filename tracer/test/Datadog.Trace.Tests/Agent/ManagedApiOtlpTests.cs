// <copyright file="ManagedApiOtlpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
    [Theory]
#if !NETCOREAPP3_1_OR_GREATER // gRPC uses OtlpGrpcRequestFactory otherwise, see GetTraces_WithGrpcProtocol_CallsTraceServiceExportMethod
    [InlineData("grpc",          "http://otel-collector:4317", "http://otel-collector:4317")]
#endif
    [InlineData("http/protobuf", "http://otel-collector:4318", "http://otel-collector:4318/v1/traces")]
    [InlineData("http/json",     "http://otel-collector:4318", "http://otel-collector:4318/v1/traces")]
    public void GetTraces_WithGeneralOtlpEndpoint_AppendsV1TracesPathOnlyForHttpProtocols(
        string protocol, string generalEndpoint, string expectedEndpoint)
    {
        var source = BuildSource(
            $"OTEL_EXPORTER_OTLP_PROTOCOL:{protocol}",
            $"OTEL_EXPORTER_OTLP_ENDPOINT:{generalEndpoint}",
            "OTEL_TRACES_EXPORTER:otlp");
        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri(expectedEndpoint));

        var factory = OtlpTransportStrategy.GetTraces(exporterSettings);
        factory.GetEndpoint(null).Should().Be(new Uri(expectedEndpoint));
    }

    [Theory]
#if !NETCOREAPP3_1_OR_GREATER // gRPC uses OtlpGrpcRequestFactory otherwise, see GetTraces_WithGrpcProtocol_CallsTraceServiceExportMethod
    [InlineData("grpc",          "http://traces-endpoint:4317/explicit")]
#endif
    [InlineData("http/protobuf", "http://traces-endpoint:4318/v1/traces/explicit")]
    [InlineData("http/json",     "http://traces-endpoint:4318/v1/traces/explicit")]
    public void GetTraces_WithExplicitTracesEndpoint_UsesEndpointAsIs(string protocol, string signalEndpoint)
    {
        // The signal-specific endpoint is always used as-is. The tracer never appends /v1/traces
        // to a customer-provided traces endpoint, regardless of protocol.
        var source = BuildSource(
            $"OTEL_EXPORTER_OTLP_PROTOCOL:{protocol}",
            "OTEL_EXPORTER_OTLP_ENDPOINT:http://general-endpoint:4318",
            $"OTEL_EXPORTER_OTLP_TRACES_ENDPOINT:{signalEndpoint}",
            "OTEL_TRACES_EXPORTER:otlp");
        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri(signalEndpoint));

        var factory = OtlpTransportStrategy.GetTraces(exporterSettings);

#if NETCOREAPP3_1_OR_GREATER
        factory.Should().BeOfType<HttpClientRequestFactory>();
#else
        factory.Should().BeOfType<ApiWebRequestFactory>();
#endif
        factory.GetEndpoint(null).Should().Be(new Uri(signalEndpoint));
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [InlineData("http/protobuf", "http://localhost:4318/v1/traces")]
    [InlineData("http/json",     "http://localhost:4318/v1/traces")]
    public void GetTraces_WhenApmUsesUnixDomainSocket_UsesDefaultOtlpEndpoint(string protocol, string expectedEndpoint)
    {
        // APM UDS exists at the default path, but no OTLP endpoint is configured.
        // The OTLP factory must NOT route through the APM UDS, and the default OTLP
        // endpoint must include /v1/traces for HTTP protocols. See GetTraces_WithGrpcProtocol_CallsTraceServiceExportMethod for grpc.
        var source = BuildSource(
            $"OTEL_EXPORTER_OTLP_PROTOCOL:{protocol}",
            "OTEL_TRACES_EXPORTER:otlp");
        Func<string, bool> fileExists = path => path == ExporterSettings.DefaultTracesUnixDomainSocket;
        var exporterSettings = new ExporterSettings(source, fileExists, NullConfigurationTelemetry.Instance);

        exporterSettings.TracesTransport.Should().Be(TracesTransportType.UnixDomainSocket);
        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri(expectedEndpoint));

        var factory = OtlpTransportStrategy.GetTraces(exporterSettings);

        // The factory must be a plain HttpClientRequestFactory, not a
        // SocketHandlerRequestFactory (which would route through the APM UDS)
        factory.Should().BeOfType<HttpClientRequestFactory>();
        factory.GetEndpoint(null).Should().Be(new Uri(expectedEndpoint));
    }

    [Theory]
#if NET6_0_OR_GREATER // See ExporterSettings_WithGrpcProtocolAndUdsEndpoint_FallsBackToDefaultTcpEndpoint otherwise
    [InlineData("grpc")]
#endif
    [InlineData("http/protobuf")]
    [InlineData("http/json")]
    public void OtlpTracesEndpoint_WithUdsGeneralEndpoint_DoesNotAppendV1TracesPath(string protocol)
    {
        // /v1/{signal} must not be appended to a unix:// general endpoint, since the URI represents a socket file path.
        // The path component will be added to the http://localhost base URI depending on the protocol.
        var source = BuildSource(
            $"OTEL_EXPORTER_OTLP_PROTOCOL:{protocol}",
            "OTEL_EXPORTER_OTLP_ENDPOINT:unix:///var/run/datadog/otlp.socket",
            "OTEL_TRACES_EXPORTER:otlp");
        var exporterSettings = new ExporterSettings(source, _ => true, NullConfigurationTelemetry.Instance);

        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri("unix:///var/run/datadog/otlp.socket"));
    }

    [Theory]
    [InlineData("http/protobuf", "http://localhost/v1/traces")]
    [InlineData("http/json",     "http://localhost/v1/traces")]
    public void GetTraces_WithUdsGeneralEndpoint_AppendsV1TracesPathOnlyForHttpProtocols(string protocol, string expectedEndpoint)
    {
        // HttpClient only accepts http(s) URIs - the UDS factory exposes http://localhost as the base
        // and appends /v1/traces for HTTP protocols. See GetTraces_WithGrpcProtocolAndUdsEndpoint_CallsTraceServiceExportMethodOverUds for grpc.
        var source = BuildSource(
            $"OTEL_EXPORTER_OTLP_PROTOCOL:{protocol}",
            "OTEL_EXPORTER_OTLP_ENDPOINT:unix:///var/run/datadog/otlp.socket",
            "OTEL_TRACES_EXPORTER:otlp");
        var exporterSettings = new ExporterSettings(source, _ => true, NullConfigurationTelemetry.Instance);

        var factory = OtlpTransportStrategy.GetTraces(exporterSettings);

#if NET6_0_OR_GREATER
        factory.Should().BeOfType<SocketHandlerRequestFactory>();
#else
        factory.Should().BeOfType<HttpStreamRequestFactory>();
#endif
        factory.GetEndpoint(null).Should().Be(new Uri(expectedEndpoint));
    }

    [Theory]
    [InlineData("http/protobuf", "http://localhost/v1/traces")]
    [InlineData("http/json",     "http://localhost/v1/traces")]
    public void GetTraces_WithUdsSignalEndpoint_AppendsV1TracesPathOnlyForHttpProtocols(string protocol, string expectedEndpoint)
    {
        // OTEL_EXPORTER_OTLP_TRACES_ENDPOINT is used as-is (no /v1/traces appended to OtlpTracesEndpoint),
        // but the UDS factory still surfaces /v1/traces in its base endpoint for HTTP protocols.
        var source = BuildSource(
            $"OTEL_EXPORTER_OTLP_PROTOCOL:{protocol}",
            "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT:unix:///var/run/datadog/otlp.socket",
            "OTEL_TRACES_EXPORTER:otlp");
        var exporterSettings = new ExporterSettings(source, _ => true, NullConfigurationTelemetry.Instance);

        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri("unix:///var/run/datadog/otlp.socket"));

        var factory = OtlpTransportStrategy.GetTraces(exporterSettings);

#if NET6_0_OR_GREATER
        factory.Should().BeOfType<SocketHandlerRequestFactory>();
#else
        factory.Should().BeOfType<HttpStreamRequestFactory>();
#endif
        factory.GetEndpoint(null).Should().Be(new Uri(expectedEndpoint));
    }

    [Theory]
    [InlineData(null, "http://localhost:4317", "http://localhost:4317")]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT:http://otel-collector:4317", "http://otel-collector:4317", "http://otel-collector:4317")]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT:http://traces-endpoint:4317/explicit", "http://traces-endpoint:4317/explicit", "http://traces-endpoint:4317/explicit")]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT:http://traces-endpoint:4317" + OtlpTransportStrategy.TracesGrpcMethodPath, "http://traces-endpoint:4317" + OtlpTransportStrategy.TracesGrpcMethodPath, "http://traces-endpoint:4317")]
    public void GetTraces_WithGrpcProtocol_CallsTraceServiceExportMethod(string endpointConfig, string expectedEndpoint, string expectedGrpcBaseEndpoint)
    {
        // Like OpenTelemetry .NET, the gRPC method path is appended to the endpoint unless it's already there
        var source = endpointConfig is null
                         ? BuildSource("OTEL_EXPORTER_OTLP_PROTOCOL:grpc", "OTEL_TRACES_EXPORTER:otlp")
                         : BuildSource("OTEL_EXPORTER_OTLP_PROTOCOL:grpc", "OTEL_TRACES_EXPORTER:otlp", endpointConfig);
        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        exporterSettings.TracesEncoding.Should().Be(TracesEncoding.OtlpProtobuf);
        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri(expectedEndpoint));

        var factory = OtlpTransportStrategy.GetTraces(exporterSettings);

        factory.Should().BeOfType<OtlpGrpcRequestFactory>();
        factory.GetEndpoint(null).Should().Be(new Uri(expectedGrpcBaseEndpoint + OtlpTransportStrategy.TracesGrpcMethodPath));
    }

    [Fact]
    public void GetMetrics_WithGrpcProtocol_CallsMetricsServiceExportMethod()
    {
        var source = BuildSource(
            "OTEL_EXPORTER_OTLP_PROTOCOL:grpc",
            "OTEL_EXPORTER_OTLP_ENDPOINT:http://otel-collector:4317");
        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        var factory = OtlpTransportStrategy.GetMetrics(exporterSettings);

        factory.Should().BeOfType<OtlpGrpcRequestFactory>();
        factory.GetEndpoint(null).Should().Be(new Uri("http://otel-collector:4317" + OtlpTransportStrategy.MetricsGrpcMethodPath));
    }

#if NET6_0_OR_GREATER
    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")]
    public void GetTraces_WithGrpcProtocolAndUdsEndpoint_CallsTraceServiceExportMethodOverUds(string endpointKey)
    {
        // HttpClient only accepts http(s) URIs - the factory uses http://localhost as the base, and connects to the socket
        var source = BuildSource(
            "OTEL_EXPORTER_OTLP_PROTOCOL:grpc",
            $"{endpointKey}:unix:///var/run/datadog/otlp.socket",
            "OTEL_TRACES_EXPORTER:otlp");
        var exporterSettings = new ExporterSettings(source, _ => true, NullConfigurationTelemetry.Instance);

        var factory = OtlpTransportStrategy.GetTraces(exporterSettings);

        factory.Should().BeOfType<OtlpGrpcRequestFactory>();
        factory.GetEndpoint(null).Should().Be(new Uri("http://localhost" + OtlpTransportStrategy.TracesGrpcMethodPath));
        factory.Info(factory.GetEndpoint(null)).Should().EndWith("to /var/run/datadog/otlp.socket");
    }
#else
    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")]
    public void ExporterSettings_WithGrpcProtocolAndUdsEndpoint_FallsBackToDefaultTcpEndpoint(string endpointKey)
    {
        // The .NET Core 3.1 build, also used on .NET 5, doesn't support gRPC over Unix Domain Sockets
        var source = BuildSource(
            "OTEL_EXPORTER_OTLP_PROTOCOL:grpc",
            $"{endpointKey}:unix:///var/run/datadog/otlp.socket",
            "OTEL_TRACES_EXPORTER:otlp");
        var exporterSettings = new ExporterSettings(source, _ => true, NullConfigurationTelemetry.Instance);

        exporterSettings.OtlpTracesEndpoint.Should().Be(new Uri("http://localhost:4317"));
        exporterSettings.ValidationWarnings.Should().Contain(w => w.Contains("doesn't support gRPC over UDS"));
        OtlpTransportStrategy.GetTraces(exporterSettings).GetEndpoint(null).Should().Be(new Uri("http://localhost:4317" + OtlpTransportStrategy.TracesGrpcMethodPath));
    }
#endif

#else
    [Fact]
    public void ExporterSettings_WithGrpcProtocol_FallsBackToDatadogEncoding()
    {
        // gRPC requires HTTP/2, which isn't available on .NET Framework and .NET Standard 2.0
        var source = BuildSource(
            "OTEL_EXPORTER_OTLP_PROTOCOL:grpc",
            "OTEL_TRACES_EXPORTER:otlp");
        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        exporterSettings.TracesEncoding.Should().Be(TracesEncoding.DatadogV0_4);
    }

#endif
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
