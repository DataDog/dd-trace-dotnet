// <copyright file="MockHttpRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using Datadog.Trace.HttpOverStreams;
using HttpHeaders = System.Net.Http.Headers.HttpHeaders;

namespace Datadog.Trace.TestHelpers;

public class MockHttpRequest
{
    public MockHeaders Headers { get; init; }

    public string Method { get; init; }

    public string PathAndQuery { get; init; }

    public long? ContentLength { get; init; }

    public Stream Body { get; init; }

    // Opt in from RequestReceived. Retain the existing read buffer before decoding, without another read or copy.
    public RequestDiagnostics Diagnostics { get; set; }

    public static MockHttpRequest Create(HttpListenerRequest request)
    {
        var headers = new MockHeaders();

        foreach (var key in request.Headers.AllKeys)
        {
            foreach (var value in request.Headers.GetValues(key))
            {
                headers.Add(key, value);
            }
        }

        return new MockHttpRequest
        {
            Headers = headers,
            Method = request.HttpMethod,
            PathAndQuery = request.Url?.PathAndQuery,
            ContentLength = request.ContentLength64,
            Body = request.InputStream, // HttpListener de-chunks a chunked body for us automatically
        };
    }

    internal byte[] ReadStreamBody()
    {
        var isGzip = Headers.TryGetValue("Content-Encoding", out var encoding) && encoding is "gzip";

        byte[] bytes = null;
        if (ContentLength is > 0)
        {
            bytes = ReadBytes(ContentLength.Value, Body, Diagnostics);
            if (!isGzip)
            {
                return bytes;
            }
        }

        // yes this is a bit horrible, but without it we get weirdness in .NET FX/netcoreapp2.1
        // where the copy doesn't actually read to the end
        using MemoryStream ms = bytes is not null ? new(bytes) : new();
        if (bytes is null)
        {
            try
            {
                Body.CopyTo(ms);
            }
            finally
            {
                if (Diagnostics is { } diagnostics)
                {
                    diagnostics.Body = new ArraySegment<byte>(ms.GetBuffer(), 0, checked((int)ms.Length));
                }
            }

            ms.Position = 0;
        }

        if (!isGzip)
        {
            return ms.ToArray();
        }

        using var finalStream = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionMode.Decompress, leaveOpen: true))
        {
            gzip.CopyTo(finalStream);
            gzip.Flush();
        }

        return finalStream.ToArray();

        static byte[] ReadBytes(long length, Stream stream, RequestDiagnostics diagnostics)
        {
            var i = 0;
            var body = new byte[length];

            try
            {
                while (i < length)
                {
                    var read = stream.Read(body, i, body.Length - i);

                    i += read;

                    if (read == 0)
                    {
                        break;
                    }
                }
            }
            finally
            {
                if (diagnostics is not null)
                {
                    // A truncated body must not include the unread, zero-filled portion of the buffer.
                    diagnostics.Body = new ArraySegment<byte>(body, 0, i);
                }
            }

            if (i < length)
            {
                throw new Exception($"Less bytes were sent than we counted. {i} read versus {length} expected.");
            }

            return body;
        }
    }

    public class RequestDiagnostics
    {
        public DateTimeOffset ReceivedAt { get; } = DateTimeOffset.UtcNow;

        public ArraySegment<byte> Body { get; internal set; }

        public string DecodedBodyInJson { get; internal set; }

        public Exception Exception { get; internal set; }

        // Populated by the TCP agent when it attempts to send a response; null if no response was attempted.
        public int? ResponseStatusCode { get; internal set; }
    }

    public class MockHeaders : HttpHeaders
    {
        public string GetValue(string name)
        {
            foreach (var value in GetValues(name))
            {
                return value;
            }

            return null;
        }

        public bool TryGetValue(string name, out string value)
        {
            if (TryGetValues(name, out var values))
            {
                foreach (var val in values)
                {
                    value = val;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
