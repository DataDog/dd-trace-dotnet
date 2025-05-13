// <copyright file="HttpStreamResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamResponse : IApiResponse
    {
        private readonly Encoding _encoding;
        private readonly HttpHeaders _headers;

        public HttpStreamResponse(int statusCode, long contentLength, Encoding encoding, Stream responseStream, HttpHeaders headers)
        {
            StatusCode = statusCode;
            ContentLength = contentLength;
            ResponseStream = responseStream;
            _encoding = encoding;
            _headers = headers;
        }

        public int StatusCode { get; }

        public long ContentLength { get; }

        public string? ContentTypeHeader => _headers.GetValue("Content-Type");

        public string? ContentEncodingHeader => _headers.GetValue("Content-Encoding");

        public Stream ResponseStream { get; }

        public void Dispose()
        {
        }

        public string? GetHeader(string headerName) => _headers.GetValue(headerName);

        public Encoding GetCharsetEncoding() => _encoding;

        public ContentEncodingType GetContentEncodingType() => ApiResponseExtensions.GetContentEncodingType(ContentEncodingHeader);

        public Task<Stream> GetStreamAsync()
        {
            return Task.FromResult(ResponseStream);
        }
    }
}
