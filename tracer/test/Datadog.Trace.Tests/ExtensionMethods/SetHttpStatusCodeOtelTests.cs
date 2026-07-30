// <copyright file="SetHttpStatusCodeOtelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ExtensionMethods;

public class SetHttpStatusCodeOtelTests
{
    private static readonly MutableSettings DefaultSettings =
        MutableSettings.CreateForTesting(new TracerSettings(), new Dictionary<string, object?>());

    // ─── http.response.status_code ───────────────────────────────────────────────

    [Fact]
    public void SetHttpStatusCode_OtelMode_SetsResponseStatusCodeTag()
    {
        var span = CreateSpan();
        span.SetHttpStatusCode(200, isServer: true, DefaultSettings, otelSemanticsEnabled: true);

        span.GetTag("http.response.status_code").Should().Be("200");
    }

    [Fact]
    public void SetHttpStatusCode_NonOtelMode_DoesNotSetResponseStatusCodeTag()
    {
        var span = CreateSpan();
        span.SetHttpStatusCode(200, isServer: true, DefaultSettings, otelSemanticsEnabled: false);

        span.GetTag("http.response.status_code").Should().BeNull();
    }

    // ─── error.type via SetErrorType ─────────────────────────────────────────────

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void SetHttpStatusCode_OtelMode_ServerSpan_SetsErrorTypeFor5xx_ByDefault(int statusCode)
    {
        var span = CreateSpan();
        span.SetHttpStatusCode(statusCode, isServer: true, DefaultSettings, otelSemanticsEnabled: true);

        span.GetTag("error.type").Should().Be(statusCode.ToString());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(422)]
    public void SetHttpStatusCode_OtelMode_ServerSpan_DoesNotSetErrorTypeFor4xx_ByDefault(int statusCode)
    {
        var span = CreateSpan();
        span.SetHttpStatusCode(statusCode, isServer: true, DefaultSettings, otelSemanticsEnabled: true);

        span.GetTag("error.type").Should().BeNull();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(422)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void SetHttpStatusCode_OtelMode_ServerSpan_SetsErrorTypeForServerErrorStatuses(int statusCode)
    {
        var userSettings = MutableSettings.CreateForTesting(new TracerSettings(), new Dictionary<string, object?> { { "DD_TRACE_HTTP_SERVER_ERROR_STATUSES", "400-599" } });

        var span = CreateSpan();
        span.SetHttpStatusCode(statusCode, isServer: true, userSettings, otelSemanticsEnabled: true);

        span.GetTag("error.type").Should().Be(statusCode.ToString());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    // [InlineData(500)] // TODO: In OTel semantic mode, 5xx status codes SHOULD be considered errors for client spans
    public void SetHttpStatusCode_OtelMode_ClientSpan_SetsErrorTypeFor4xxAnd5xx_ByDefault(int statusCode)
    {
        var span = CreateSpan();
        span.SetHttpStatusCode(statusCode, isServer: false, DefaultSettings, otelSemanticsEnabled: true);

        span.GetTag("error.type").Should().Be(statusCode.ToString());
    }

    [Theory]
    [InlineData(200)]
    [InlineData(301)]
    [InlineData(399)]
    public void SetHttpStatusCode_OtelMode_ClientSpan_DoesNotSetErrorTypeBelow400_ByDefault(int statusCode)
    {
        var span = CreateSpan();
        span.SetHttpStatusCode(statusCode, isServer: false, DefaultSettings, otelSemanticsEnabled: true);

        span.GetTag("error.type").Should().BeNull();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(422)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void SetHttpStatusCode_OtelMode_ClientSpan_SetsErrorTypeForServerErrorStatuses(int statusCode)
    {
        var userSettings = MutableSettings.CreateForTesting(new TracerSettings(), new Dictionary<string, object?> { { "DD_TRACE_HTTP_CLIENT_ERROR_STATUSES", "400-599" } });

        var span = CreateSpan();
        span.SetHttpStatusCode(statusCode, isServer: false, userSettings, otelSemanticsEnabled: true);

        span.GetTag("error.type").Should().Be(statusCode.ToString());
    }

    [Fact]
    public void SetHttpStatusCode_OtelMode_DoesNotOverwriteExistingErrorType()
    {
        var span = CreateSpan();
        span.SetTag("error.type", "System.Net.Http.HttpRequestException");
        span.SetHttpStatusCode(500, isServer: true, DefaultSettings, otelSemanticsEnabled: true);

        span.GetTag("error.type").Should().Be("System.Net.Http.HttpRequestException");
    }

    [Fact]
    public void SetHttpStatusCode_NonOtelMode_DoesNotSetErrorType()
    {
        var span = CreateSpan();
        span.SetHttpStatusCode(500, isServer: true, DefaultSettings, otelSemanticsEnabled: false);

        span.GetTag("error.type").Should().BeNull();
    }

    private static Span CreateSpan()
    {
        var traceContext = new TraceContext(new StubDatadogTracer());
        var spanContext = new SpanContext(parent: null, traceContext, serviceName: null);
        return new Span(spanContext, DateTimeOffset.UtcNow);
    }
}
