// <copyright file="AzureFrontDoorSpanFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Proxy;

public class AzureFrontDoorSpanFactoryTests
{
    [Fact]
    public async Task CreateSpan_CreatesSpanWithCorrectProperties()
    {
        var factory = new AzureFrontDoorSpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var startTime = DateTimeOffset.UtcNow;
        var data = new InferredProxyData("azure-fd", startTime, "myapp.azurefd.net", "GET", "api/v1/users", null, null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        var span = scope!.Span;
        span.Should().NotBeNull();
        span.OperationName.Should().Be("azure.frontdoor");
        span.ResourceName.Should().Be("GET /api/v1/users");
        span.Type.Should().Be("web");
        span.StartTime.Should().Be(startTime);

        var tags = scope.Span.Tags;
        tags.Should().NotBeNull();
        tags.GetTag(Tags.HttpMethod).Should().Be("GET");
        tags.GetTag(Tags.InstrumentationName).Should().Be("azure-fd");
        tags.GetTag(Tags.HttpUrl).Should().Be("myapp.azurefd.net/api/v1/users");
        tags.GetTag(Tags.HttpRoute).Should().Be("/api/v1/users");
    }

    [Fact]
    public async Task CreateSpan_WithNoPath_TreatsItAsTheRootPath()
    {
        // Front Door's {url_path} omits the leading slash, so a request to the site root arrives as
        // an empty header value, which ParseUtility surfaces as null. That is the root path, not an
        // absent one, so the route and resource name must be "/" rather than empty.
        // https://learn.microsoft.com/en-us/azure/frontdoor/rule-set-server-variables
        var factory = new AzureFrontDoorSpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var data = new InferredProxyData("azure-fd", DateTimeOffset.UtcNow, "myapp.azurefd.net", "GET", path: null, null, null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        var tags = scope!.Span.Tags;
        tags.GetTag(Tags.HttpRoute).Should().Be("/");
        tags.GetTag(Tags.HttpUrl).Should().Be("myapp.azurefd.net/");
        scope.Span.ResourceName.Should().Be("GET /");
    }

    [Fact]
    public async Task CreateSpan_WithNullHttpMethod_UsesRouteAsResourceName()
    {
        // With no method, the resource name should fall back to just the route (no leading space
        // or stray "null"), and the method tag should be absent.
        var factory = new AzureFrontDoorSpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var data = new InferredProxyData("azure-fd", DateTimeOffset.UtcNow, "myapp.azurefd.net", httpMethod: null, "api/test", null, null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        scope!.Span.ResourceName.Should().Be("/api/test");
        scope.Span.Tags.GetTag(Tags.HttpMethod).Should().BeNull();
    }

    // The route is lower-cased and id-quantized for aggregation, while http.url keeps the path as
    // the proxy sent it. Front Door's {url_path} has no leading slash, so we prepend one - without
    // double-slashing a path that already has it, should that ever change.
    [Theory]
    [InlineData("api/v1/users", "/api/v1/users", "myapp.azurefd.net/api/v1/users")]
    [InlineData("/api/test", "/api/test", "myapp.azurefd.net/api/test")]
    [InlineData("Api/Test", "/api/test", "myapp.azurefd.net/Api/Test")]
    [InlineData("api/users/12345", "/api/users/?", "myapp.azurefd.net/api/users/12345")]
    public async Task CreateSpan_NormalizesPathIntoRouteAndUrl(string path, string expectedRoute, string expectedUrl)
    {
        var factory = new AzureFrontDoorSpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var data = new InferredProxyData("azure-fd", DateTimeOffset.UtcNow, "myapp.azurefd.net", "GET", path, null, null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        scope!.Span.Tags.GetTag(Tags.HttpRoute).Should().Be(expectedRoute);
        scope.Span.Tags.GetTag(Tags.HttpUrl).Should().Be(expectedUrl);
        scope.Span.ResourceName.Should().Be($"GET {expectedRoute}");
    }
}
