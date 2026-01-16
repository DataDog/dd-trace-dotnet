// <copyright file="AzureProxyExtractorTests.cs" company="Datadog">
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

public class AzureProxyExtractorTests
{
    private readonly AzureApiManagementExtractor _extractor = new();

    [Fact]
    public void TryExtract_WithAllValidHeaders_ReturnsTrue()
    {
        var start = DateTimeOffset.UtcNow;
        var startTimeString = start.ToString("O", CultureInfo.InvariantCulture);

        var headers = CreateValidAzureHeaders(startTimeString);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("azure-apim");
        data.StartTime.Should().Be(start);
        data.DomainName.Should().BeNull(); // Azure doesn't use domain
        data.HttpMethod.Should().Be("POST");
        data.Path.Should().Be("/api/v1/users");
        data.Stage.Should().BeNull(); // Azure doesn't use stage
    }

    [Fact]
    public void TryExtract_WithMinimumValidHeaders_ReturnsTrue()
    {
        var start = DateTimeOffset.UtcNow;
        var startTimeString = start.ToString("O", CultureInfo.InvariantCulture);

        var headers = CreateValidAzureHeaders(startTimeString);
        headers.Remove(InferredProxyHeaders.HttpMethod);
        headers.Remove(InferredProxyHeaders.Path);
        headers.Remove(InferredProxyHeaders.Region);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("azure-apim");
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
    [InlineData("not-a-date")]
    [InlineData("12345")] // not ISO 8601
    public void TryExtract_WithInvalidStartTime_ReturnsFalse(string startTime)
    {
        var headers = CreateValidAzureHeaders();
        headers.Set(InferredProxyHeaders.StartTime, startTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Fact]
    public void TryExtract_WithMissingStartTime_ReturnsFalse()
    {
        var headers = CreateValidAzureHeaders();
        headers.Remove(InferredProxyHeaders.StartTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Fact]
    public void TryExtract_WithCustomHeaderValues_ExtractsAllData()
    {
        var start = DateTimeOffset.UtcNow;
        var startTimeString = start.ToString("O", CultureInfo.InvariantCulture);

        var headers = CreateValidAzureHeaders(startTimeString);
        headers.Set(InferredProxyHeaders.HttpMethod, "PUT");
        headers.Set(InferredProxyHeaders.Path, "/api/v2/orders/123");
        headers.Set(InferredProxyHeaders.Region, "east us");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("azure-apim");
        data.StartTime.Should().Be(start);
        data.HttpMethod.Should().Be("PUT");
        data.Path.Should().Be("/api/v2/orders/123");
    }

    [Theory]
    [InlineData("2021-01-01T00:00:00.000Z", "2021-01-01T00:00:00.000Z")] // UTC with milliseconds
    [InlineData("2025-12-03T14:21:01.1900116Z", "2025-12-03T14:21:01.1900116Z")] // High precision
    [InlineData("2020-06-15T10:30:45Z", "2020-06-15T10:30:45Z")] // Without milliseconds
    public void TryExtract_WithValidStartTimestamps_ReturnsTrue(string startTime, string expectedTime)
    {
        var headers = CreateValidAzureHeaders(startTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.StartTime.Should().Be(DateTimeOffset.Parse(expectedTime, CultureInfo.InvariantCulture));
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
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("OPTIONS")]
    [InlineData("HEAD")]
    public void TryExtract_WithDifferentHttpMethods_ExtractsCorrectly(string httpMethod)
    {
        var headers = CreateValidAzureHeaders();
        headers.Set(InferredProxyHeaders.HttpMethod, httpMethod);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.HttpMethod.Should().Be(httpMethod);
    }

    [Fact]
    public void TryExtract_WithComplexPath_ExtractsCorrectly()
    {
        var headers = CreateValidAzureHeaders();
        headers.Set(InferredProxyHeaders.Path, "/api/v1/customers/456/orders/789/items");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.Path.Should().Be("/api/v1/customers/456/orders/789/items");
    }

    [Fact]
    public void TryExtract_WithQueryParametersInPath_ExtractsCorrectly()
    {
        var headers = CreateValidAzureHeaders();
        headers.Set(InferredProxyHeaders.Path, "/api/search?filter=active&sort=name&limit=50");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.Path.Should().Be("/api/search?filter=active&sort=name&limit=50");
    }

    [Theory]
    [InlineData("canada central")]
    [InlineData("east us")]
    [InlineData("west europe")]
    [InlineData("southeast asia")]
    [InlineData("australia east")]
    public void TryExtract_WithDifferentRegions_ExtractsCorrectly(string region)
    {
        var headers = CreateValidAzureHeaders();
        headers.Set(InferredProxyHeaders.Region, region);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        // Note: Region is extracted but not stored in InferredProxyData (it's logged only)
    }

    [Fact]
    public void TryExtract_WithRootPath_ExtractsCorrectly()
    {
        var headers = CreateValidAzureHeaders();
        headers.Set(InferredProxyHeaders.Path, "/");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.Path.Should().Be("/");
    }

    [Fact]
    public void TryExtract_WithPathOnly_ExtractsCorrectly()
    {
        var start = DateTimeOffset.UtcNow;
        var startTimeString = start.ToString("O", CultureInfo.InvariantCulture);
        var headers = CreateValidAzureHeaders(startTimeString);
        headers.Remove(InferredProxyHeaders.HttpMethod);
        headers.Remove(InferredProxyHeaders.Region);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.Path.Should().Be("/api/v1/users");
        data.HttpMethod.Should().BeNull();
    }

    [Fact]
    public void TryExtract_WithMethodOnly_ExtractsCorrectly()
    {
        var start = DateTimeOffset.UtcNow;
        var startTimeString = start.ToString("O", CultureInfo.InvariantCulture);
        var headers = CreateValidAzureHeaders(startTimeString);
        headers.Remove(InferredProxyHeaders.Path);
        headers.Remove(InferredProxyHeaders.Region);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.HttpMethod.Should().Be("POST");
        data.Path.Should().BeNull();
    }

    private static NameValueHeadersCollection CreateValidAzureHeaders(string start = null)
    {
        start ??= DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var headers = new NameValueHeadersCollection([]);
        headers.Set(InferredProxyHeaders.Name, "azure-apim");
        headers.Set(InferredProxyHeaders.StartTime, start);
        headers.Set(InferredProxyHeaders.HttpMethod, "POST");
        headers.Set(InferredProxyHeaders.Path, "/api/v1/users");
        headers.Set(InferredProxyHeaders.Region, "canada central");
        return headers;
    }
}
