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
        var data = new InferredProxyData("azure-fd", startTime, "myapp.azurefd.net", "GET", "/api/v1/users", null, null);

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
        tags.GetTag(Tags.InstrumentationName).Should().Be("azure-frontdoor");
        tags.GetTag(Tags.HttpUrl).Should().Be("myapp.azurefd.net/api/v1/users");
        tags.GetTag(Tags.HttpRoute).Should().Be("/api/v1/users");
    }
}
