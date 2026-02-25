// <copyright file="InferredProxySpanHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Proxy;

public class InferredProxySpanHelperTests : IAsyncLifetime
{
    private readonly Tracer _tracer;

    public InferredProxySpanHelperTests()
    {
        _tracer = ProxyTestHelpers.GetMockTracer();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _tracer.DisposeAsync();

    [Fact]
    public void ExtractAndCreateInferredProxyScope_WithAwsHeaders_ReturnsAwsScope()
    {
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
        var headers = ProxyTestHelpers.CreateValidHeaders(unixTimeMilliseconds.ToString());

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            new PropagationContext());

        result.Should().NotBeNull();
        result!.Value.Scope.Should().NotBeNull();
        result.Value.Scope.Span.OperationName.Should().Be("aws.apigateway");
        result.Value.Scope.Span.StartTime.Should().Be(start);
    }

    [Fact]
    public void ExtractAndCreateInferredProxyScope_WithAzureHeaders_ReturnsAzureScope()
    {
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
        var headers = ProxyTestHelpers.CreateValidAzureHeaders(unixTimeMilliseconds.ToString());

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            new PropagationContext());

        result.Should().NotBeNull();
        result!.Value.Scope.Should().NotBeNull();
        result.Value.Scope.Span.OperationName.Should().Be("azure.apim");
        result.Value.Scope.Span.StartTime.Should().Be(start);
    }

    [Fact]
    public void ExtractAndCreateInferredProxyScope_WithMissingProxyNameHeader_ReturnsNull()
    {
        var headers = ProxyTestHelpers.CreateValidHeaders();
        headers.Remove(InferredProxyHeaders.Name);

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            new PropagationContext());

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-proxy")]
    [InlineData("aws.apigateway")]
    [InlineData("azure.apim")]
    [InlineData("gcp-api-gateway")]
    public void ExtractAndCreateInferredProxyScope_WithInvalidProxyName_ReturnsNull(string proxyName)
    {
        var headers = ProxyTestHelpers.CreateValidHeaders();
        headers.Set(InferredProxyHeaders.Name, proxyName);

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            new PropagationContext());

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("aws-apigateway")]
    [InlineData("AWS-APIGATEWAY")]
    [InlineData("Aws-ApiGateway")]
    public void ExtractAndCreateInferredProxyScope_WithAwsProxyName_CaseInsensitive_ReturnsAwsScope(string proxyName)
    {
        var headers = ProxyTestHelpers.CreateValidHeaders();
        headers.Set(InferredProxyHeaders.Name, proxyName);

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            new PropagationContext());

        result.Should().NotBeNull();
        result!.Value.Scope.Span.OperationName.Should().Be("aws.apigateway");
    }

    [Theory]
    [InlineData("azure-apim")]
    [InlineData("AZURE-APIM")]
    [InlineData("Azure-Apim")]
    public void ExtractAndCreateInferredProxyScope_WithAzureProxyName_CaseInsensitive_ReturnsAzureScope(string proxyName)
    {
        var unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
        var headers = ProxyTestHelpers.CreateValidAzureHeaders(unixTimeMilliseconds.ToString());
        headers.Set(InferredProxyHeaders.Name, proxyName);

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            new PropagationContext());

        result.Should().NotBeNull();
        result!.Value.Scope.Span.OperationName.Should().Be("azure.apim");
    }

    [Fact]
    public void ExtractAndCreateInferredProxyScope_WithAwsHeadersMissingStartTime_ReturnsNull()
    {
        var headers = ProxyTestHelpers.CreateValidHeaders();
        headers.Remove(InferredProxyHeaders.StartTime);

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            new PropagationContext());

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractAndCreateInferredProxyScope_WithAzureHeadersMissingStartTime_ReturnsNull()
    {
        var headers = ProxyTestHelpers.CreateValidAzureHeaders();
        headers.Remove(InferredProxyHeaders.StartTime);

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            new PropagationContext());

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractAndCreateInferredProxyScope_UpdatesPropagationContext()
    {
        var headers = ProxyTestHelpers.CreateValidHeaders();
        var originalContext = new PropagationContext(null, []);

        var result = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(
            _tracer,
            headers,
            originalContext);

        result.Should().NotBeNull();
        result!.Value.Context.SpanContext.Should().NotBeNull();
        result.Value.Context.SpanContext.Should().BeEquivalentTo(result.Value.Scope.Span.Context);
        result.Value.Context.Baggage.Should().BeEmpty();
    }
}
