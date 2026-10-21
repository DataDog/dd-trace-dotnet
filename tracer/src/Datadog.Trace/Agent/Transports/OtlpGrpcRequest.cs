// <copyright file="OtlpGrpcRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
#if !NET5_0_OR_GREATER
using System.Reflection;
#endif

namespace Datadog.Trace.Agent.Transports;

/// <summary>
/// Sends an OTLP export request, already serialized as protobuf, as a unary gRPC call.
/// Only <see cref="PostAsync(ArraySegment{byte}, string)"/> is supported, which is all <see cref="ApiOtlp"/> needs.
/// </summary>
internal sealed class OtlpGrpcRequest : IApiRequest
{
#if !NET5_0_OR_GREATER
    // This build also runs on .NET 5, where the version policy is set with reflection, as it doesn't exist in .NET Core 3.1
    private static readonly PropertyInfo? VersionPolicyProperty = typeof(HttpRequestMessage).GetProperty("VersionPolicy");
    private static readonly object? RequestVersionExactPolicy = GetRequestVersionExactPolicy();
#endif

    private readonly HttpClient _client;
    private readonly HttpRequestMessage _request;

    public OtlpGrpcRequest(HttpClient client, Uri endpoint)
    {
        _client = client;
        _request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Version = HttpVersion.Version20 };

        // Required to send HTTP/2 requests without TLS since .NET 5
#if NET5_0_OR_GREATER
        _request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
#else
        if (RequestVersionExactPolicy is not null)
        {
            VersionPolicyProperty!.SetValue(_request, RequestVersionExactPolicy);
        }
#endif

        // Some servers abort gRPC calls without this header
        _request.Headers.TryAddWithoutValidation("TE", "trailers");
    }

    public void AddHeader(string name, string value) => _request.Headers.Add(name, value);

    public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType) => PostAsync(bytes, contentType, null);

    public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string? contentEncoding)
    {
        if (contentType != MimeTypes.XProtobuf || !StringUtil.IsNullOrEmpty(contentEncoding))
        {
            throw new NotSupportedException($"gRPC requests only support uncompressed {MimeTypes.XProtobuf} payloads.");
        }

        using var content = new GrpcMessageContent(bytes);
        _request.Content = content;

        // The whole response must be read for its trailers, which contain the gRPC status, to be available
        var response = await _client.SendAsync(_request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
        return new OtlpGrpcResponse(response);
    }

    public Task<IApiResponse> GetAsync() => throw new NotSupportedException();

    public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression) => throw new NotSupportedException();

    public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings) => throw new NotSupportedException();

    public Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary) => throw new NotSupportedException();

    public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None) => throw new NotSupportedException();

#if !NET5_0_OR_GREATER
    private static object? GetRequestVersionExactPolicy()
    {
        try
        {
            // HttpVersionPolicy.RequestVersionExact
            return VersionPolicyProperty is null ? null : Enum.Parse(VersionPolicyProperty.PropertyType, "RequestVersionExact");
        }
        catch
        {
            // Can't happen with the actual API, but only gRPC with TLS would work then, as on .NET Core 3.1
            return null;
        }
    }
#endif

    /// <summary>
    /// A gRPC length-prefixed message: a compressed flag, the message length as a big-endian 32-bit integer, then the message.
    /// </summary>
    private sealed class GrpcMessageContent : HttpContent
    {
        private const int PrefixLength = 5;
        private static readonly MediaTypeHeaderValue GrpcContentType = new("application/grpc");

        private readonly ArraySegment<byte> _message;

        public GrpcMessageContent(ArraySegment<byte> message)
        {
            _message = message;
            Headers.ContentType = GrpcContentType;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            // The first byte is the compressed flag, left to 0 as the message isn't compressed
            var prefix = new byte[PrefixLength];
            BinaryPrimitives.WriteUInt32BigEndian(prefix.AsSpan(1), (uint)_message.Count);

            await stream.WriteAsync(prefix, 0, PrefixLength).ConfigureAwait(false);
            await stream.WriteAsync(_message.Array!, _message.Offset, _message.Count).ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = PrefixLength + _message.Count;
            return true;
        }
    }
}
#endif
