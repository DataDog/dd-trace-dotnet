// <copyright file="AzureFrontDoorExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Headers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Proxy;

public class AzureFrontDoorExtractorTests
{
    private readonly AzureFrontDoorExtractor _extractor;

    public AzureFrontDoorExtractorTests()
    {
        _extractor = new AzureFrontDoorExtractor();
    }

    [Fact]
    public void TryExtract_WithAllValidHeaders_ReturnsTrue()
    {
        // this reduces precision to 1ms, so we can't compare extracted value to the original DateTimeOffset directly
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders(unixTimeMilliseconds.ToString());

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("azure-fd");
        data.StartTime.Should().Be(start);
        data.DomainName.Should().Be("myapp.azurefd.net");
        data.HttpMethod.Should().Be("GET");
        data.Path.Should().Be("/api/test");
        data.Stage.Should().Be("prod");
        data.Region.Should().Be("canada central");
    }

    [Fact]
    public void TryExtract_WithMinimumValidHeaders_ReturnsTrue()
    {
        // this reduces precision to 1ms, so we can't compare extracted value to the original DateTimeOffset directly
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
        
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders(unixTimeMilliseconds.ToString());
        headers.Remove(InferredProxyHeaders.HttpMethod);
        headers.Remove(InferredProxyHeaders.Path);
        headers.Remove(InferredProxyHeaders.Region);
        headers.Remove(InferredProxyHeaders.Stage);
        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("azure-fd");
        data.StartTime.Should().Be(start);
        data.DomainName.Should().Be("myapp.azurefd.net");
        data.HttpMethod.Should().BeNull();
        data.Path.Should().BeNull();
        data.Stage.Should().BeNull();
        data.Region.Should().BeNull();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("1111111122222222333333334444444455555555666666667777777788888888")] // too large
    public void TryExtract_WithInvalidStartTime_ReturnsFalse(string startTime)
    {
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Set(InferredProxyHeaders.StartTime, startTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
    }

    [Fact]
    public void TryExtract_WithMissingStartTime_ReturnsTrue()
    {
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Remove(InferredProxyHeaders.StartTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
    }
}
