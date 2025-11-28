// <copyright file="AwsProxyExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Headers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Proxy;

public class AwsProxyExtractorTests
{
    private readonly AwsApiGatewayExtractor _extractor = new();

    [Fact]
    public void TryExtract_WithAllValidHeaders_ReturnsTrue()
    {
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

        var headers = CreateValidAwsHeaders(unixTimeMilliseconds.ToString(CultureInfo.InvariantCulture));

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("aws-apigateway");
        data.StartTime.Should().Be(start);
        data.DomainName.Should().Be("test.execute-api.us-east-1.amazonaws.com");
        data.HttpMethod.Should().Be("GET");
        data.Path.Should().Be("/prod/api/users");
        data.Stage.Should().Be("prod");
    }

    [Fact]
    public void TryExtract_WithMinimumValidHeaders_ReturnsTrue()
    {
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

        var headers = CreateValidAwsHeaders(unixTimeMilliseconds.ToString(CultureInfo.InvariantCulture));
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
        var headers = CreateValidAwsHeaders();
        headers.Set(InferredProxyHeaders.StartTime, startTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Fact]
    public void TryExtract_WithMissingStartTime_ReturnsFalse()
    {
        var headers = CreateValidAwsHeaders();
        headers.Remove(InferredProxyHeaders.StartTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Fact]
    public void TryExtract_WithCustomDomainAndStage_ExtractsAllData()
    {
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

        var headers = CreateValidAwsHeaders(unixTimeMilliseconds.ToString(CultureInfo.InvariantCulture));
        headers.Set(InferredProxyHeaders.Domain, "my-custom-domain.com");
        headers.Set(InferredProxyHeaders.HttpMethod, "POST");
        headers.Set(InferredProxyHeaders.Path, "/v2/orders");
        headers.Set(InferredProxyHeaders.Stage, "staging");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("aws-apigateway");
        data.StartTime.Should().Be(start);
        data.DomainName.Should().Be("my-custom-domain.com");
        data.HttpMethod.Should().Be("POST");
        data.Path.Should().Be("/v2/orders");
        data.Stage.Should().Be("staging");
    }

    [Theory]
    [InlineData("0")] // Unix epoch
    [InlineData("1609459200000")] // 2021-01-01 00:00:00 UTC
    [InlineData("9999999999999")] // Far future date
    public void TryExtract_WithValidStartTimestamps_ReturnsTrue(string startTime)
    {
        var headers = CreateValidAwsHeaders(startTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.StartTime.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(startTime)));
    }

    [Fact]
    public void TryExtract_WithEmptyHeaders_ReturnsFalse()
    {
        var headers = new NameValueHeadersCollection([]);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("OPTIONS")]
    [InlineData("HEAD")]
    public void TryExtract_WithDifferentHttpMethods_ExtractsCorrectly(string httpMethod)
    {
        var headers = CreateValidAwsHeaders();
        headers.Set(InferredProxyHeaders.HttpMethod, httpMethod);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.HttpMethod.Should().Be(httpMethod);
    }

    [Fact]
    public void TryExtract_WithComplexPath_ExtractsCorrectly()
    {
        var headers = CreateValidAwsHeaders();
        headers.Set(InferredProxyHeaders.Path, "/v1/users/123/orders/456/items");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.Path.Should().Be("/v1/users/123/orders/456/items");
    }

    [Fact]
    public void TryExtract_WithQueryParametersInPath_ExtractsCorrectly()
    {
        var headers = CreateValidAwsHeaders();
        headers.Set(InferredProxyHeaders.Path, "/api/search?query=test&limit=10");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.Path.Should().Be("/api/search?query=test&limit=10");
    }

    [Theory]
    [InlineData("dev")]
    [InlineData("staging")]
    [InlineData("production")]
    [InlineData("v1")]
    public void TryExtract_WithDifferentStages_ExtractsCorrectly(string stage)
    {
        var headers = CreateValidAwsHeaders();
        headers.Set(InferredProxyHeaders.Stage, stage);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.Stage.Should().Be(stage);
    }

    [Fact]
    public void TryExtract_WithRegionalDomain_ExtractsCorrectly()
    {
        var headers = CreateValidAwsHeaders();
        headers.Set(InferredProxyHeaders.Domain, "api.execute-api.eu-west-1.amazonaws.com");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.DomainName.Should().Be("api.execute-api.eu-west-1.amazonaws.com");
    }

    private static NameValueHeadersCollection CreateValidAwsHeaders(string start = null)
    {
        start ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        var headers = new NameValueHeadersCollection([]);
        headers.Set(InferredProxyHeaders.Name, "aws-apigateway");
        headers.Set(InferredProxyHeaders.StartTime, start);
        headers.Set(InferredProxyHeaders.Domain, "test.execute-api.us-east-1.amazonaws.com");
        headers.Set(InferredProxyHeaders.HttpMethod, "GET");
        headers.Set(InferredProxyHeaders.Path, "/prod/api/users");
        headers.Set(InferredProxyHeaders.Stage, "prod");
        return headers;
    }
}
