// <copyright file="InferredProxyCoordinatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Proxy;

public class InferredProxyCoordinatorTests : IAsyncLifetime
{
    private readonly Mock<IInferredProxyExtractor> _extractor;
    private readonly Mock<IInferredSpanFactory> _factory;
    private readonly ScopedTracer _tracer; // this is a mock instance
    private readonly InferredProxyCoordinator _coordinator;

    public InferredProxyCoordinatorTests()
    {
        _extractor = new Mock<IInferredProxyExtractor>();
        _factory = new Mock<IInferredSpanFactory>();
        _tracer = ProxyTestHelpers.GetMockTracer();
        _coordinator = new InferredProxyCoordinator(_extractor.Object, _factory.Object);
    }

    private delegate void TryExtractCallback(
        NameValueHeadersCollection carrier,
        HeadersCollectionAccesor<NameValueHeadersCollection> carrierGetter,
        out InferredProxyData data);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _tracer.DisposeAsync();

    [Fact]
    public void ExtractAndCreateScope_WhenExtractorReturnsFalse_ShouldReturnNull()
    {
        var headers = new NameValueHeadersCollection(new System.Collections.Specialized.NameValueCollection());
        _extractor.Setup(e => e.TryExtract(
                      It.IsAny<NameValueHeadersCollection>(),
                      It.IsAny<HeadersCollectionAccesor<NameValueHeadersCollection>>(),
                      out It.Ref<InferredProxyData>.IsAny))
                  .Returns(false);

        var result = _coordinator.ExtractAndCreateScope(
            _tracer,
            headers,
            headers.GetAccessor(),
            new PropagationContext());

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractAndCreateScope_WhenSpanFactoryReturnsNull_ShouldReturnNull()
    {
        // header values not important
        var headers = ProxyTestHelpers.CreateValidHeaders();
        var proxyData = new InferredProxyData("aws-apigateway", DateTimeOffset.UtcNow, "test.api.com", "GET", "/api/test", "prod", null);

        _extractor.Setup(e => e.TryExtract(
                      It.IsAny<NameValueHeadersCollection>(),
                      It.IsAny<HeadersCollectionAccesor<NameValueHeadersCollection>>(),
                      out proxyData))
                  .Returns(true); // we successfully extract headers

        // and then fail creating the span within the factory (for some reason)
        _factory.Setup(f => f.CreateSpan(It.IsAny<Tracer>(), It.IsAny<InferredProxyData>(), It.IsAny<ISpanContext>())).Returns((Scope?)null);

        var result = _coordinator.ExtractAndCreateScope(_tracer, headers, headers.GetAccessor(), new PropagationContext());

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractAndCreateScope_WhenExtractorReturnsTrue_ShouldReturnScope()
    {
        var headers = new NameValueHeadersCollection([]);
        var startTime = DateTimeOffset.UtcNow;
        var proxyData = new InferredProxyData(
            "aws-apigateway",
            startTime,
            "test.api.com",
            "GET",
            "/api/test",
            "prod",
            null);

        _extractor.Setup(e => e.TryExtract(
                      It.IsAny<NameValueHeadersCollection>(),
                      It.IsAny<HeadersCollectionAccesor<NameValueHeadersCollection>>(),
                      out It.Ref<InferredProxyData>.IsAny))
                  .Returns(true)
                  .Callback(new TryExtractCallback((
                      NameValueHeadersCollection _,
                      HeadersCollectionAccesor<NameValueHeadersCollection> _,
                      out InferredProxyData data) =>
                  {
                      data = proxyData;
                  }));

        // using an actual scope that the factor will return
        using var realScope = _tracer.StartActiveInternal("test.operation");
        _factory.Setup(f => f.CreateSpan(
                    It.IsAny<Tracer>(),
                    It.Is<InferredProxyData>(d =>
                        d.ProxyName == proxyData.ProxyName &&
                        d.StartTime == proxyData.StartTime),
                    It.IsAny<ISpanContext>()))
                .Returns(realScope);

        var result = _coordinator.ExtractAndCreateScope(
            _tracer,
            headers,
            headers.GetAccessor(),
            new PropagationContext())!;

        result.Should().NotBeNull();
        result.Value.Scope.Should().Be(realScope);
        result.Value.Context.SpanContext.Should().BeEquivalentTo(realScope.Span.Context);
    }
}
