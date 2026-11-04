// <copyright file="ApiSecurityTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Security.Unit.Tests.Iast;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.ApiSec;

public class ApiSecurityTelemetryTests
{
    [Theory]
    [InlineData("aspnet_core", "framework:aspnet_core")]
    [InlineData("aspnet", "framework:aspnet")]
    [InlineData("other", "framework:unknown")]
    [InlineData(null, "framework:unknown")]
    public async Task GivenARequestWithoutRoute_WhenSampling_ThenTheMissingRouteIsReported(string? component, string expectedTag)
    {
        var metrics = await SampleAsync(route: null, statusCode: 200, component: component);

        metrics.Should().ContainSingle()
               .Which.Should().BeEquivalentTo(("api_security.missing_route", new[] { expectedTag }));
    }

    [Theory]
    [InlineData(null, 404, false, true, SamplingPriorityValues.AutoKeep)]
    [InlineData(null, 403, true, true, SamplingPriorityValues.AutoKeep)]
    [InlineData(null, 200, false, false, SamplingPriorityValues.AutoKeep)]
    [InlineData(null, 200, false, true, SamplingPriorityValues.AutoReject)]
    public async Task GivenARequestThatHasNoRouteToMiss_WhenSampling_ThenNothingIsReported(string? route, int statusCode, bool blocked, bool lastWafCall, int samplingPriority)
    {
        var metrics = await SampleAsync(route, statusCode, blocked: blocked, lastWafCall: lastWafCall, samplingPriority: samplingPriority);

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenARequestWithRoute_WhenSampling_ThenNoMissingRouteIsReported()
    {
        var metrics = await SampleAsync(route: "api/values", statusCode: 200);

        metrics.Should().NotContain(m => m.Name == "api_security.missing_route");
    }

    [Fact]
    public async Task GivenARequestWithoutRoute_WhenTheLastWafRunBlocksIt_ThenNothingIsReported()
    {
        var result = new Mock<IResult>();
        result.SetupGet(x => x.ShouldBlock).Returns(true);

        var metrics = await SampleAsync(route: null, statusCode: 200, result: result.Object);

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenASampledRequestWithSchemas_WhenRecorded_ThenTheSchemaIsReported()
    {
        var result = new Mock<IResult>();
        result.SetupGet(x => x.ExtractSchemaDerivatives).Returns(new Dictionary<string, object?> { { "_dd.appsec.s.req.headers", "[]" } });

        var metrics = await RecordSchemaAsync(result.Object);

        metrics.Should().ContainSingle()
               .Which.Should().BeEquivalentTo(("api_security.request.schema", new[] { "framework:aspnet_core" }));
    }

    [Theory]
    [CombinatorialData]
    public async Task GivenASampledRequestWithoutSchemas_WhenRecorded_ThenNoSchemaIsReported(bool nullResult)
    {
        var metrics = await RecordSchemaAsync(nullResult ? null : new Mock<IResult>().Object);

        metrics.Should().ContainSingle()
               .Which.Should().BeEquivalentTo(("api_security.request.no_schema", new[] { "framework:aspnet_core" }));
    }

    private static async Task<List<(string Name, string[] Tags)>> SampleAsync(string? route, int statusCode, string? component = "aspnet_core", bool blocked = false, bool lastWafCall = true, int samplingPriority = SamplingPriorityValues.AutoKeep, IResult? result = null)
    {
        var config = new Dictionary<string, object> { { ConfigurationKeys.AppSec.ApiSecurityEnabled, true } };
        await using var tracer = TracerHelper.Create(TracerSettings.Create(config));
        var apiSecurity = new ApiSecurity(new SecuritySettings(new CustomSettingsForTests(config), new NullConfigurationTelemetry()));
        var span = CreateSpan(tracer, samplingPriority, component);
        span.SetTag(Tags.HttpRoute, route);
        span.SetTag(Tags.HttpMethod, "GET");
        if (blocked)
        {
            span.SetTag(Tags.AppSecBlocked, "true");
        }

        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var decision = apiSecurity.GetSamplingDecision(lastWafCall, span, new Dictionary<string, object>(), statusCode, routeValues: null);
        ApiSecurity.RecordSamplingTelemetry(decision, result, span, collector);
        return await GetMetricsAsync(collector);
    }

    private static async Task<List<(string Name, string[] Tags)>> RecordSchemaAsync(IResult? result)
    {
        await using var tracer = TracerHelper.Create(new());
        var span = CreateSpan(tracer, SamplingPriorityValues.AutoKeep, "aspnet_core");
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        ApiSecurity.RecordSamplingTelemetry(ApiSecurity.SamplingDecision.Sample, result, span, collector);
        return await GetMetricsAsync(collector);
    }

    private static Span CreateSpan(ScopedTracer tracer, int samplingPriority, string? component)
    {
        var traceContext = new TraceContext(tracer, new TraceTagCollection());
        traceContext.SetSamplingPriority(samplingPriority);
        var span = new Span(new SpanContext(SpanContext.None, traceContext, "Test"), DateTimeOffset.Now);
        span.SetTag(Tags.InstrumentationName, component);
        return span;
    }

    private static async Task<List<(string Name, string[] Tags)>> GetMetricsAsync(MetricsTelemetryCollector collector)
    {
        await collector.DisposeAsync();
        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, m.Tags ?? []))
                        .ToList()
            ?? [];
    }
}
