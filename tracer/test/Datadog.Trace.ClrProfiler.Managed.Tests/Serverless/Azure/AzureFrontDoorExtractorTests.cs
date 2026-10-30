// <copyright file="AzureFrontDoorExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        data.Path.Should().Be("api/test");
        data.Stage.Should().Be("prod");
        data.Region.Should().Be("canada central");
    }

    [Fact]
    public void TryExtract_WithNoOptionalHeaders_ReturnsTrueWithNullFields()
    {
        // Every header except the proxy name is optional; absence must not fail extraction, and the
        // fields should stay null (not empty string) so the factory can tell "not provided" apart.
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();
        headers.Remove(InferredProxyHeaders.Domain);
        headers.Remove(InferredProxyHeaders.HttpMethod);
        headers.Remove(InferredProxyHeaders.Path);
        headers.Remove(InferredProxyHeaders.Region);
        headers.Remove(InferredProxyHeaders.Stage);

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("azure-fd");
        data.DomainName.Should().BeNull();
        data.HttpMethod.Should().BeNull();
        data.Path.Should().BeNull();
        data.Stage.Should().BeNull();
        data.Region.Should().BeNull();
    }

    // Front Door injects no timestamp header, and it forwards client headers by default, so every
    // one of these means "the proxy gave us no usable start time". Extraction must still succeed
    // with the start time synthesized from now - if an unparseable value failed extraction instead,
    // a caller could suppress the inferred span entirely by sending a garbage timestamp.
    [Theory]
    [InlineData(null)]                                                               // header absent
    [InlineData("")]                                                                 // present but empty
    [InlineData("invalid")]                                                          // not a number
    [InlineData("999999999999999")]                                                  // parses as long, out of DateTimeOffset range
    public void TryExtract_WithNoUsableStartTime_SynthesizesStartTimeAndReturnsTrue(string startTime)
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var headers = ProxyTestHelpers.CreateValidAzureFrontDoorHeaders();

        if (startTime is null)
        {
            headers.Remove(InferredProxyHeaders.StartTime);
        }
        else
        {
            headers.Set(InferredProxyHeaders.StartTime, startTime);
        }

        var success = _extractor.TryExtract(headers, headers.GetAccessor(), out var data);
        var after = DateTimeOffset.UtcNow.AddSeconds(5);

        success.Should().BeTrue();
        data.ProxyName.Should().Be("azure-fd");
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
}
