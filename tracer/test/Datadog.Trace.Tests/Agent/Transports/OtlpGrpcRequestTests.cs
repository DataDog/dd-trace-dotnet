// <copyright file="OtlpGrpcRequestTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent.Transports;

public class OtlpGrpcRequestTests
{
    private static readonly Uri Endpoint = new("http://localhost:4317" + OtlpTransportStrategy.TracesGrpcMethodPath);

    [Fact]
    public async Task PostAsync_SendsLengthPrefixedMessageOverHttp2()
    {
        var handler = new FakeHandler(() => CreateResponse(grpcStatus: "0"));
        var factory = new OtlpGrpcRequestFactory(Endpoint, [new("api-key", "secret")], TimeSpan.FromSeconds(10), handler);

        using var response = await factory.Create(Endpoint).PostAsync(new ArraySegment<byte>([0xFF, 1, 2, 3, 0xFF], 1, 3), MimeTypes.XProtobuf);

        response.StatusCode.Should().Be(200);

        var request = handler.Request!;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.Should().Be(Endpoint);
        request.Version.Should().Be(HttpVersion.Version20);
#if NET6_0_OR_GREATER
        request.VersionPolicy.Should().Be(HttpVersionPolicy.RequestVersionExact);
#endif
        request.Headers.TE.Should().ContainSingle(x => x.Value == "trailers");
        request.Headers.GetValues("api-key").Should().Equal("secret");
        handler.ContentType.Should().Be("application/grpc");

        // Not compressed, then the message length as a big-endian 32-bit integer, then the message
        handler.Content.Should().Equal(0, 0, 0, 0, 3, 1, 2, 3);
    }

    [Theory]
    [InlineData("0", 200)] // OK
    [InlineData("14", 503)] // UNAVAILABLE, retryable
    [InlineData("4", 503)] // DEADLINE_EXCEEDED, retryable
    [InlineData("8", 429)] // RESOURCE_EXHAUSTED, retried like an HTTP 429
    [InlineData("3", 500)] // INVALID_ARGUMENT, not retryable
    [InlineData("12", 500)] // UNIMPLEMENTED, not retryable
    [InlineData(null, 500)] // grpc-status is missing
    public async Task PostAsync_MapsGrpcStatusToHttpStatusCode(string? grpcStatus, int expectedStatusCode)
    {
        var factory = new OtlpGrpcRequestFactory(Endpoint, [], TimeSpan.FromSeconds(10), new FakeHandler(() => CreateResponse(grpcStatus)));

        using var response = await factory.Create(Endpoint).PostAsync(new ArraySegment<byte>([1]), MimeTypes.XProtobuf);

        response.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task PostAsync_ReadsGrpcStatusFromHeadersOfTrailersOnlyResponse()
    {
        var handler = new FakeHandler(
            () =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
                response.Headers.Add("grpc-status", "14");
                return response;
            });
        var factory = new OtlpGrpcRequestFactory(Endpoint, [], TimeSpan.FromSeconds(10), handler);

        using var response = await factory.Create(Endpoint).PostAsync(new ArraySegment<byte>([1]), MimeTypes.XProtobuf);

        response.StatusCode.Should().Be(503);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task PostAsync_ReturnsHttpStatusCodeOfNonGrpcResponse(HttpStatusCode statusCode)
    {
        // gRPC servers always reply with a 200 status code, so other ones come from a proxy or a non-gRPC server
        var handler = new FakeHandler(() => new HttpResponseMessage(statusCode) { Content = new StringContent("not gRPC") });
        var factory = new OtlpGrpcRequestFactory(Endpoint, [], TimeSpan.FromSeconds(10), handler);

        using var response = await factory.Create(Endpoint).PostAsync(new ArraySegment<byte>([1]), MimeTypes.XProtobuf);

        response.StatusCode.Should().Be((int)statusCode);
        (await response.ReadAsStringAsync()).Should().Be("not gRPC");
    }

    [Fact]
    public async Task PostAsync_DescribesGrpcStatusOfFailedCall()
    {
        // The gRPC status message is percent-encoded
        var factory = new OtlpGrpcRequestFactory(Endpoint, [], TimeSpan.FromSeconds(10), new FakeHandler(() => CreateResponse("14", "collector%20is%20down")));

        using var response = await factory.Create(Endpoint).PostAsync(new ArraySegment<byte>([1]), MimeTypes.XProtobuf);

        (await response.ReadAsStringAsync()).Should().Be("gRPC status Unavailable: collector is down");
    }

    [Theory]
    [InlineData(MimeTypes.Json, null)]
    [InlineData(MimeTypes.XProtobuf, "gzip")]
    public async Task PostAsync_OnlySupportsUncompressedProtobuf(string contentType, string? contentEncoding)
    {
        var factory = new OtlpGrpcRequestFactory(Endpoint, [], TimeSpan.FromSeconds(10), new FakeHandler(() => CreateResponse("0")));

        var post = () => factory.Create(Endpoint).PostAsync(new ArraySegment<byte>([1]), contentType, contentEncoding);

        await post.Should().ThrowAsync<NotSupportedException>();
    }

#if !NET6_0_OR_GREATER
    [Fact]
    public void CreatingFactory_DoesNotChangeHttp2UnencryptedSupport()
    {
        // .NET Core 3.1 only allows HTTP/2 without TLS for the factory's own handler
        var wasEnabled = AppContext.TryGetSwitch(PlatformKeys.AppContextHttp2UnencryptedSupport, out var enabled) && enabled;

        _ = new OtlpGrpcRequestFactory(Endpoint, [], TimeSpan.FromSeconds(10));

        (AppContext.TryGetSwitch(PlatformKeys.AppContextHttp2UnencryptedSupport, out var enabledAfter) && enabledAfter).Should().Be(wasEnabled);
    }
#endif

    private static HttpResponseMessage CreateResponse(string? grpcStatus, string? grpcMessage = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        if (grpcStatus is not null)
        {
            response.TrailingHeaders.Add("grpc-status", grpcStatus);
        }

        if (grpcMessage is not null)
        {
            response.TrailingHeaders.Add("grpc-message", grpcMessage);
        }

        return response;
    }

    private sealed class FakeHandler(Func<HttpResponseMessage> createResponse) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? ContentType { get; private set; }

        public byte[]? Content { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The content is disposed once the request is sent, so capture it now
            Request = request;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Content = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync();

            var response = createResponse();
            response.RequestMessage = request;
            return response;
        }
    }
}
#endif
