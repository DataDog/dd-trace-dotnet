// <copyright file="MockHttpRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;

namespace Datadog.Trace.TestHelpers;

internal class MockHttpRequest
{
    public HttpHeaders Headers { get; set; } = new HttpHeaders();

    public string Method { get; set; }

    public string PathAndQuery { get; set; }

    public long? ContentLength { get; set; }

    public StreamContent Body { get; set; }

    public static MockHttpRequest Create(HttpListenerRequest request)
    {
        var headers = new HttpHeaders(request.Headers.Count);

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
            Body = new StreamContent(request.InputStream, request.ContentLength64),
        };
    }

    internal byte[] ReadStreamBody()
    {
        return ContentLength is { } length
                   ? ReadBytes(length, Body.Stream)
                   : ReadChunked(Body.Stream);

        static byte[] ReadChunked(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            // Does another copy but meh
            return ms.ToArray();
        }

        static byte[] ReadBytes(long length, Stream stream)
        {
            var i = 0;
            var body = new byte[length];

            while (i < length)
            {
                var read = stream.Read(body, i, body.Length - i);

                i += read;

                if (read == 0 || read == body.Length)
                {
                    break;
                }
            }

            if (i < length)
            {
                throw new Exception($"Less bytes were sent than we counted. {i} read versus {length} expected.");
            }

            return body;
        }
    }
}
