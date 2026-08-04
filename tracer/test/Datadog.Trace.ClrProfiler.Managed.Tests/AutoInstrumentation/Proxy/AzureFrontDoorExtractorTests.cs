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
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Remove(InferredProxyHeaders.HttpMethod);
        headers.Remove(InferredProxyHeaders.Path);
        headers.Remove(InferredProxyHeaders.Region);
        headers.Remove(InferredProxyHeaders.Stage);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("azure-fd");
        data.DomainName.Should().Be("myapp.azurefd.net");
        data.HttpMethod.Should().BeNull();
        data.Path.Should().BeNull();
        data.Stage.Should().BeNull();
        data.Region.Should().BeNull();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("not-a-number")]
    [InlineData("1111111122222222333333334444444455555555666666667777777788888888")] // too large
    public void TryExtract_WithInvalidStartTime_ReturnsFalseAndDefaultData(string startTime)
    {
        // A non-empty but unparseable start time is not synthesized away; extraction must fail
        // and leave `data` untouched (default) so no partial/garbage span is created.
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Set(InferredProxyHeaders.StartTime, startTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeFalse();
        data.Should().Be(default(InferredProxyData));
    }

    [Fact]
    public void TryExtract_WithMissingStartTime_ReturnsTrue()
    {
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Remove(InferredProxyHeaders.StartTime);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
    }

    [Fact]
    public void TryExtract_WithEmptyStartTime_SynthesizesStartTimeAndReturnsTrue()
    {
        // Unlike APIM, Front Door does not emit a start-time header, so a missing/empty value is
        // expected and must be synthesized from "now" rather than causing extraction to fail.
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Set(InferredProxyHeaders.StartTime, string.Empty);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);
        var after = DateTimeOffset.UtcNow.AddSeconds(5);

        success.Should().BeTrue();
        data.StartTime.Should().NotBe(default);
        data.StartTime.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void TryExtract_WithLowerCaseHttpMethod_NormalizesToUpperCase()
    {
        // The HTTP method must be normalized so downstream resource names / tags are stable
        // regardless of the casing the proxy happens to send.
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Set(InferredProxyHeaders.HttpMethod, "post");

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.HttpMethod.Should().Be("POST");
    }

    [Fact]
    public void TryExtract_WithMissingDomain_ReturnsTrueWithNullDomain()
    {
        // Domain is optional; its absence must not fail extraction, and the field should stay null
        // (not empty string) so the factory can distinguish "not provided".
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Remove(InferredProxyHeaders.Domain);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.DomainName.Should().BeNull();
    }
}
