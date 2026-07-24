// <copyright file="ApiOtlpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class ApiOtlpTests
{
    [Fact]
    public async Task SendStatsAsync_WhenSpanMetricsDisabled_DoesNotPostToMetricsEndpoint()
    {
        var source = BuildSource(
            "OTEL_TRACES_EXPORTER:otlp",
            "OTEL_TRACES_SPAN_METRICS_ENABLED:false");

        var settings = new TracerSettings(source);
        settings.OtelTracesSpanMetricsEnabled.Should().BeFalse();

        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        var mockTracesFactory = new Mock<IApiRequestFactory>();
        mockTracesFactory.Setup(f => f.GetEndpoint(null)).Returns(new Uri("http://localhost:4317"));

        var mockMetricsRequest = new Mock<IApiRequest>();
        mockMetricsRequest
            .Setup(r => r.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("PostAsync should not be called when span metrics are disabled."));
        var mockMetricsFactory = new Mock<IApiRequestFactory>();
        mockMetricsFactory.Setup(f => f.Create(It.IsAny<Uri>())).Returns(mockMetricsRequest.Object);

        var api = new ApiOtlp(mockTracesFactory.Object, mockMetricsFactory.Object, settings, exporterSettings);

        var result = await api.SendStatsAsync(CreateBufferWithOneHit(), bucketDuration: 10_000_000_000L, tracerObfuscationVersion: 0);
        result.Should().BeTrue();
        mockMetricsRequest.Verify(r => r.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendStatsAsync_WhenSpanMetricsEnabled_PostsToMetricsEndpoint()
    {
        var source = BuildSource(
            "OTEL_TRACES_EXPORTER:otlp",
            "OTEL_TRACES_SPAN_METRICS_ENABLED:true");

        var settings = new TracerSettings(source);
        settings.OtelTracesSpanMetricsEnabled.Should().BeTrue();

        var exporterSettings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

        var mockResponse = new Mock<IApiResponse>();
        mockResponse.Setup(r => r.StatusCode).Returns(200);

        var mockMetricsRequest = new Mock<IApiRequest>();
        mockMetricsRequest
            .Setup(r => r.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>()))
            .ReturnsAsync(mockResponse.Object);

        var mockTracesFactory = new Mock<IApiRequestFactory>();
        mockTracesFactory.Setup(f => f.GetEndpoint(null)).Returns(new Uri("http://localhost:4317"));

        var mockMetricsFactory = new Mock<IApiRequestFactory>();
        mockMetricsFactory.Setup(f => f.Create(It.IsAny<Uri>())).Returns(mockMetricsRequest.Object);

        var api = new ApiOtlp(mockTracesFactory.Object, mockMetricsFactory.Object, settings, exporterSettings);

        var result = await api.SendStatsAsync(CreateBufferWithOneHit(), bucketDuration: 10_000_000_000L, tracerObfuscationVersion: 0);

        result.Should().BeTrue();
        mockMetricsRequest.Verify(r => r.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>()), Times.Once);
    }

    private static StatsBuffer CreateBufferWithOneHit()
    {
        var buffer = new StatsBuffer(
            new ClientStatsPayload(MutableSettings.CreateForTesting(new(), [])),
            new StatsCardinalityLimiter(new TracerSettings()),
            new StatsCardinalityReporter(NullMetricsTelemetryCollector.Instance));

        var key = new StatsAggregationKey(
            resource: "GET /",
            service: "test-service",
            operationName: "http.request",
            type: "web",
            httpStatusCode: 200,
            isSyntheticsRequest: false,
            spanKind: "server",
            isError: false,
            isTopLevel: true,
            isTraceRoot: true,
            httpMethod: "GET",
            httpEndpoint: "/",
            grpcStatusCode: string.Empty,
            serviceSource: string.Empty,
            peerTagsHash: 0,
            additionalMetricTagsHash: 0,
            truncatedFields: StatsCardinalityTruncatedFields.None);

        buffer.Buckets.Add(key, new StatsBucket(key, [], []) { Hits = 1, Duration = 5_000_000 });
        return buffer;
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
