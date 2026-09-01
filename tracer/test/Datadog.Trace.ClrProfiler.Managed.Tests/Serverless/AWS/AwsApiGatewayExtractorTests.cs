// <copyright file="AwsApiGatewayExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Headers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Proxy;

public class AwsApiGatewayExtractorTests
{
    private readonly AwsApiGatewayExtractor _extractor;

    public AwsApiGatewayExtractorTests()
    {
        _extractor = new AwsApiGatewayExtractor();
    }

    [Fact]
    public void TryExtract_WithAllValidHeaders_ReturnsTrue()
    {
        // this reduces precision to 1ms, so we can't compare extracted value to the original DateTimeOffset directly
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

        var headers = ProxyTestHelpers.CreateValidHeaders(unixTimeMilliseconds.ToString(CultureInfo.InvariantCulture));

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("aws-apigateway");
        data.StartTime.Should().Be(start);
        data.DomainName.Should().Be("test.api.com");
        data.HttpMethod.Should().Be("GET");
        data.Path.Should().Be("/api/test");
        data.Stage.Should().Be("prod");
    }

    [Fact]
    public void TryExtract_WithMinimumValidHeaders_ReturnsTrue()
    {
        // this reduces precision to 1ms, so we can't compare extracted value to the original DateTimeOffset directly
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

        var headers = ProxyTestHelpers.CreateValidHeaders(unixTimeMilliseconds.ToString(CultureInfo.InvariantCulture));
        headers.Remove(InferredProxyHeaders.Domain);
        headers.Remove(InferredProxyHeaders.HttpMethod);
        headers.Remove(InferredProxyHeaders.Path);
        headers.Remove(InferredProxyHeaders.Stage);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("aws-apigateway");
        data.StartTime.Should().Be(start);
        data.DomainName.Should().BeNull();
        data.HttpMethod.Should().BeNull();
        data.Path.Should().BeNull();
        data.Stage.Should().BeNull();
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

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Fact]
    public void TryExtract_WithMissingStartTime_ReturnsFalse()
    {
        var headers = ProxyTestHelpers.CreateValidHeaders();
        headers.Remove(InferredProxyHeaders.StartTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }
}
