// <copyright file="AzureApiManagementSpanFactoryTests.cs" company="Datadog">
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

public class AzureApiManagementSpanFactoryTests : IAsyncLifetime
{
    private readonly AzureApiManagementSpanFactory _factory;
    private readonly ScopedTracer _tracer; // this is a mocked instance of the tracer

    public AzureApiManagementSpanFactoryTests()
    {
        _factory = new AzureApiManagementSpanFactory();
        _tracer = ProxyTestHelpers.GetMockTracer();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _tracer.DisposeAsync();

    [Fact]
    public void CreateSpan_CreatesSpanWithCorrectProperties()
    {
        var startTime = DateTimeOffset.UtcNow;
        var data = new InferredProxyData("azure-apim", startTime, "test.api.azure.com", "POST", "/api/v1/users", null, null);

        var scope = _factory.CreateSpan(_tracer, data);

        scope.Should().NotBeNull();
        var span = scope!.Span;
        span.Should().NotBeNull();
        span.OperationName.Should().Be("azure.apim");
        span.ResourceName.Should().Be("POST /api/v1/users"); // TODO obfuscation and quantization
        span.Type.Should().Be("web");
        span.StartTime.Should().Be(startTime);

        var tags = scope.Span.Tags;
        tags.Should().NotBeNull();
        tags.GetTag(Tags.HttpMethod).Should().Be("POST");
        tags.GetTag(Tags.InstrumentationName).Should().Be("azure-apim");
        tags.GetTag(Tags.HttpUrl).Should().Be("test.api.azure.com/api/v1/users");
        tags.GetTag(Tags.HttpRoute).Should().Be("/api/v1/users");
    }
}
