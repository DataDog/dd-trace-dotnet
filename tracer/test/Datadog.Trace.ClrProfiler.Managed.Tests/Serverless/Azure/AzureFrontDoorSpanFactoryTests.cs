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
        span.ResourceName.Should().Be("GET /api/v1/users"); // TODO obfuscation and quantization
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
    public async Task CreateSpan_WithNullPath_ProducesEmptyRoute()
    {
        // A missing path must not throw or produce a null route; the span should still be created
        // with an empty route and a url that is just the domain.
        var factory = new AzureFrontDoorSpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var data = new InferredProxyData("azure-fd", DateTimeOffset.UtcNow, "myapp.azurefd.net", "GET", path: null, null, null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        var tags = scope!.Span.Tags;
        tags.GetTag(Tags.HttpRoute).Should().BeNullOrEmpty();
        tags.GetTag(Tags.HttpUrl).Should().Be("myapp.azurefd.net/");
        scope.Span.ResourceName.Should().Be("GET ");
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

    [Fact]
    public async Task CreateSpan_QuantizesNumericIdInPath()
    {
        // Numeric identifiers in the path must be quantized so high-cardinality ids don't explode
        // the resource/route dimension.
        var factory = new AzureFrontDoorSpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var data = new InferredProxyData("azure-fd", DateTimeOffset.UtcNow, "myapp.azurefd.net", "GET", "api/users/12345", null, null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        scope!.Span.Tags.GetTag(Tags.HttpRoute).Should().Be("/api/users/?");
        scope.Span.ResourceName.Should().Be("GET /api/users/?");
    }

    [Fact]
    public async Task CreateSpan_LowerCasesRoute()
    {
        // The route is expected to be normalized to lower case for consistent aggregation.
        var factory = new AzureFrontDoorSpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var data = new InferredProxyData("azure-fd", DateTimeOffset.UtcNow, "myapp.azurefd.net", "GET", "Api/Test", null, null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        scope!.Span.Tags.GetTag(Tags.HttpRoute).Should().Be("/api/test");
    }

    [Fact]
    public async Task CreateSpan_WithLeadingSlashPath_DoesNotDoubleSlash()
    {
        // Front Door currently sends a relative path (no leading slash), but guard against a future
        // change: an already-absolute path must not produce a double-slashed route/url ("//api/test").
        var factory = new AzureFrontDoorSpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var data = new InferredProxyData("azure-fd", DateTimeOffset.UtcNow, "myapp.azurefd.net", "GET", "/api/test", null, null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        scope!.Span.Tags.GetTag(Tags.HttpRoute).Should().Be("/api/test");
        scope.Span.Tags.GetTag(Tags.HttpUrl).Should().Be("myapp.azurefd.net/api/test");
        scope.Span.ResourceName.Should().Be("GET /api/test");
    }
}
