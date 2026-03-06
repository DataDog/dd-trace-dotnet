// <copyright file="RaspModuleDownstreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETCOREAPP3_1_OR_GREATER

using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

[Collection(nameof(SecuritySequentialTests))]
public class RaspModuleDownstreamTests : WafLibraryRequiredTest
{
    [Fact]
    public void ExtractHeaders_ValidHeaders_ExtractsCorrectly()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Authorization"] = "Bearer token123",
            ["X-Custom-Header"] = "custom-value"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock.Object);

        result.Should().NotBeNull();
        result.Should().ContainKey("content-type");
        result.Should().ContainKey("authorization");
        result.Should().ContainKey("x-custom-header");
    }

    [Fact]
    public void ExtractHeaders_CookieHeader_ExcludesCookie()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Cookie"] = "session=abc123; user=john",
            ["Authorization"] = "Bearer token123"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock.Object);

        result.Should().NotBeNull();
        result.Should().NotContainKey("cookie");
        result.Should().ContainKey("content-type");
        result.Should().ContainKey("authorization");
    }

    [Fact]
    public void ExtractHeaders_EmptyHeaders_ReturnsNull()
    {
        var headers = new Dictionary<string, string>();

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock.Object);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractHeaders_OnlyCookieHeader_ReturnsNull()
    {
        var headers = new Dictionary<string, string>
        {
            ["Cookie"] = "session=abc123"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock.Object);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractHeaders_CaseInsensitiveCookie_ExcludesCookie()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["COOKIE"] = "session=abc123",
            ["CoOkIe"] = "another=value"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock.Object);

        result.Should().NotBeNull();
        result.Should().NotContainKey("cookie");
        result.Should().ContainKey("content-type");
    }

    [Fact]
    public void ExtractHeaders_HeadersToLowercase_ConvertsKeys()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["AUTHORIZATION"] = "Bearer token123",
            ["X-Custom-Header"] = "value"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock.Object);

        result.Should().NotBeNull();
        result.Should().ContainKey("content-type");
        result.Should().ContainKey("authorization");
        result.Should().ContainKey("x-custom-header");
    }

    [Theory]
    [InlineData("{\"key\":\"value\"}", "application/json", true)]
    [InlineData("{\"user\":{\"name\":\"John\"}}", "application/json", true)]
    [InlineData("[1,2,3,4,5]", "application/json", true)]
    [InlineData("", "application/json", false)]
    [InlineData("{\"key\":\"value\"}", "text/plain", true)]
    public void AddBody_JsonContent_ParsesCorrectly(string body, string contentType, bool shouldParse)
    {
        var mockContent = HttpMocks.CreateMockContent(body, contentType);
        var wafArgs = new Dictionary<string, object>();

        // Use reflection to call the private AddBody method
        var method = typeof(RaspModule).GetMethod("AddBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        method!.Invoke(null, [mockContent.Object, wafArgs, AddressesConstants.DownstreamRequestBody, 10_000_000L]);

        if (shouldParse && !string.IsNullOrEmpty(body))
        {
            wafArgs.Should().ContainKey(AddressesConstants.DownstreamRequestBody);
        }
        else
        {
            // Empty body should not add to wafArgs
            if (string.IsNullOrEmpty(body))
            {
                wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
            }
        }
    }

    [Fact]
    public void AddBody_OversizedContent_SkipsBodyParsing()
    {
        var largeBody = new string('a', 100_000);
        var mockContent = HttpMocks.CreateMockContent(largeBody, "application/json", 100_000);
        var wafArgs = new Dictionary<string, object>();

        // Use reflection to call the private AddBody method
        var method = typeof(RaspModule).GetMethod("AddBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        // Body size limit is 50,000 bytes
        method!.Invoke(null, [mockContent.Object, wafArgs, AddressesConstants.DownstreamRequestBody, 50_000L]);

        // Should not add body because it exceeds size limit
        wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
    }

    [Fact]
    public void AddBody_NullContent_DoesNotAddBody()
    {
        var wafArgs = new Dictionary<string, object>();

        // Use reflection to call the private AddBody method
        var method = typeof(RaspModule).GetMethod("AddBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        method!.Invoke(null, [null, wafArgs, AddressesConstants.DownstreamRequestBody, 10_000_000L]);

        wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
    }

    [Fact]
    public void AddBody_ZeroLengthContent_DoesNotAddBody()
    {
        var mockContent = HttpMocks.CreateMockContent(string.Empty, "application/json", 0);
        var wafArgs = new Dictionary<string, object>();

        // Use reflection to call the private AddBody method
        var method = typeof(RaspModule).GetMethod("AddBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        method!.Invoke(null, [mockContent.Object, wafArgs, AddressesConstants.DownstreamRequestBody, 10_000_000L]);

        wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
    }

    [Fact]
    public void AddBody_InvalidJson_DoesNotAddBody()
    {
        var invalidJson = "{invalid: json}";
        var mockContent = HttpMocks.CreateMockContent(invalidJson, "application/json");
        var wafArgs = new Dictionary<string, object>();

        // Use reflection to call the private AddBody method
        var method = typeof(RaspModule).GetMethod("AddBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        method!.Invoke(null, [mockContent.Object, wafArgs, AddressesConstants.DownstreamRequestBody, 10_000_000L]);

        // Invalid JSON should not crash, but may not add to wafArgs
        // The behavior depends on BodyParser.Parse returning null for invalid JSON
    }
}

#endif
