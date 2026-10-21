// <copyright file="HttpRequestBodyReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    /// <summary>
    /// Reads the body of an incoming request as text, transparently decompressing
    /// gzip-encoded bodies. The managed tracer's telemetry defaults to gzip
    /// compression, so raw readers fail to parse those payloads.
    /// </summary>
    internal static class HttpRequestBodyReader
    {
        public static string ReadBodyAsText(HttpListenerRequest request)
        {
            return ReadBodyAsText(request.InputStream, request.Headers["Content-Encoding"], request.ContentEncoding);
        }

        internal static string ReadBodyAsText(Stream body, string contentEncodingHeader, Encoding fallbackEncoding)
        {
            // The caller (or HttpListener) owns the stream; both paths leave it open.
            if (string.Equals(contentEncodingHeader, "gzip", StringComparison.OrdinalIgnoreCase))
            {
                using var decompressed = new GZipStream(body, CompressionMode.Decompress, leaveOpen: true);
                using var reader = new StreamReader(decompressed, Encoding.UTF8);
                return reader.ReadToEnd();
            }

            using var plainReader = new StreamReader(body, fallbackEncoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            return plainReader.ReadToEnd();
        }
    }
}
