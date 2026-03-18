// <copyright file="HttpMocks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Security.Unit.Tests.Utils;

internal static class HttpMocks
{
    public static HttpRequestMessage CreateMockRequest(
        string url,
        string method,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url));

        foreach (var kvp in headers ?? [])
        {
            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        if (body is not null)
        {
            request.Content = new ChunkedContent(Encoding.UTF8.GetBytes(body), contentType ?? "application/json");
        }

        return request;
    }

    public static HttpResponseMessage CreateMockResponse(
        int statusCode,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null)
    {
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)statusCode);

        foreach (var kvp in headers ?? [])
        {
            response.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        if (body is not null)
        {
            response.Content = new ChunkedContent(Encoding.UTF8.GetBytes(body), contentType ?? "application/json");
        }

        return response;
    }

    public static HttpContent CreateMockContent(string body, string contentType, long? length = null)
        => new ChunkedContent(Encoding.UTF8.GetBytes(body), contentType, length);

    /// <summary>
    /// Creates an HttpContent that exceeds the size limit but has no Content-Length header.
    /// Simulates a large chunked response.
    /// </summary>
    public static HttpContent CreateLargeChunkedContent(int sizeInBytes, string contentType, bool incomplete = false)
    {
        // Build a JSON array large enough to exceed the limit
        var sb = new StringBuilder("[");
        while (sb.Length < sizeInBytes)
        {
            sb.Append("\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",");
        }

        if (!incomplete)
        {
            sb.Append("\"end\"]");
        }

        return new ChunkedContent(Encoding.UTF8.GetBytes(sb.ToString()), contentType);
    }

    public static HttpHeaders CreateMockHeaders(Dictionary<string, string> headers)
    {
        var httpHeaders = new TestHttpHeaders();

        foreach (var kvp in headers)
        {
            httpHeaders.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        return httpHeaders;
    }

    /// <summary>
    /// HttpContent with no Content-Length (like chunked transfer encoding).
    /// </summary>
    private sealed class ChunkedContent : HttpContent
    {
        private readonly byte[] _data;

        public ChunkedContent(byte[] data, string contentType, long? length = null)
        {
            _data = data;
            Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

            if (length is > 0)
            {
                Headers.ContentLength = length;
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(_data, 0, _data.Length);

        protected override bool TryComputeLength(out long length)
        {
            // Return false to prevent the framework from computing or caching ContentLength,
            // exactly as happens with chunked transfer encoding
            length = 0;
            return false;
        }
    }

    // Concrete subclass to allow adding any header without category restrictions
    private sealed class TestHttpHeaders : HttpHeaders
    {
    }
}
