// <copyright file="AwsApiGatewayExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Proxy;

public class AwsApiGatewayExtractorTests
{
    private readonly AwsApiGatewayExtractor _extractor;
    private readonly Tracer _tracer; // this is a mocked instance of the tracer

    public AwsApiGatewayExtractorTests()
    {
        _extractor = new AwsApiGatewayExtractor();
        _tracer = ProxyTestHelpers.GetMockTracer();
    }

    [Fact]
    public void TryExtract_WhenProxySpansDisabled_ReturnsFalse()
    {
        var collection = new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled, "false" }
        };

        var tracer = ProxyTestHelpers.GetMockTracer(collection);
        var headers = ProxyTestHelpers.CreateValidHeaders(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

        var success = _extractor.TryExtract(headers, headers.GetAccesor(), tracer, out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Fact]
    public void TryExtract_WithValidHeaders_ReturnsTrue()
    {
        var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var headers = ProxyTestHelpers.CreateValidHeaders(start.ToString());

        var success = _extractor.TryExtract(headers, headers.GetAccesor(), _tracer, out var data);

        success.Should().BeTrue();

        data.ProxyName.Should().Be("aws-apigateway");
        data.StartTime.ToUnixTimeMilliseconds().Should().Be(start);
        data.DomainName.Should().Be("test.api.com");
        data.HttpMethod.Should().Be("GET");
        data.Path.Should().Be("/api/test");
        data.Stage.Should().Be("prod");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("aws.apigateway")]
    public void TryExtract_WithInvalidProxyName_ReturnsFalse(string proxyName)
    {
        var headers = ProxyTestHelpers.CreateValidHeaders();
        headers.Set(InferredProxyHeaders.Name, proxyName);

        var success = _extractor.TryExtract(headers, headers.GetAccesor(), _tracer, out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("1111111122222222333333334444444455555555666666667777777788888888")] // too large
    public void TryExtract_WithInvalidStartTime_ReturnsFalse(string startTime)
    {
        var headers = ProxyTestHelpers.CreateValidHeaders();
        headers.Set(InferredProxyHeaders.StartTime, startTime);

        var success = _extractor.TryExtract(headers, headers.GetAccesor(), _tracer, out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }
}
