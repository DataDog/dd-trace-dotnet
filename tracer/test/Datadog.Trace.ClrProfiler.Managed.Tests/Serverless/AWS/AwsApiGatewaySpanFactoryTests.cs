// <copyright file="AwsApiGatewaySpanFactoryTests.cs" company="Datadog">
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

public class AwsApiGatewaySpanFactoryTests
{
    [Fact]
    public async Task CreateSpan_CreatesSpanWithCorrectProperties()
    {
        var factory = new AwsApiGatewaySpanFactory();
        await using var tracer = ProxyTestHelpers.GetMockTracer();
        var startTime = DateTimeOffset.UtcNow;

        var data = new InferredProxyData("aws-apigateway", startTime, "test.api.com", "GET", "/api/test", "prod", null);

        var scope = factory.CreateSpan(tracer, data);

        scope.Should().NotBeNull();
        var span = scope!.Span;
        span.Should().NotBeNull();
        span.OperationName.Should().Be("aws.apigateway");
        span.ResourceName.Should().Be("GET /api/test"); // TODO obfuscation and quantization
        span.Type.Should().Be("web");
        span.StartTime.Should().Be(startTime);

        var tags = scope.Span.Tags;
        tags.Should().NotBeNull();
        tags.GetTag(Tags.HttpMethod).Should().Be("GET");
        tags.GetTag(Tags.InstrumentationName).Should().Be("aws-apigateway");
        tags.GetTag(Tags.HttpUrl).Should().Be("test.api.com/api/test");
        tags.GetTag(Tags.HttpRoute).Should().Be("/api/test");
        tags.GetTag(Tags.ProxyStage).Should().Be("prod");
    }
}
