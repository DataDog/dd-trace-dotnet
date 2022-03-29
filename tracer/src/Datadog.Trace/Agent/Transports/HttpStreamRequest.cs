// <copyright file="HttpStreamRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamRequest : IApiRequest
    {
        private readonly Uri _uri;
        private readonly DatadogHttpClient _client;
        private readonly IStreamFactory _streamFactory;
        private readonly HttpHeaders _headers = new();

        public HttpStreamRequest(DatadogHttpClient client, Uri uri, IStreamFactory streamFactory)
        {
            _uri = uri;
            _client = client;
            _streamFactory = streamFactory;
        }

        public void AddHeader(string name, string value)
        {
            _headers.Add(name, value);
        }

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType) => (await PostSegmentAsync(bytes, contentType).ConfigureAwait(false)).Item1;

        private async Task<Tuple<IApiResponse, HttpRequest>> PostSegmentAsync(ArraySegment<byte> segment, string contentType)
        {
            using (var bidirectionalStream = _streamFactory.GetBidirectionalStream())
            {
                var content = new BufferContent(segment);
                _headers.Add("Content-Type", contentType);
                var request = new HttpRequest("POST", _uri.Host, _uri.PathAndQuery, _headers, content);
                // send request, get response
                var response = await _client.SendAsync(request, bidirectionalStream, bidirectionalStream).ConfigureAwait(false);

                // Content-Length is required as we don't support chunked transfer
                var contentLength = response.Content.Length;
                if (!contentLength.HasValue)
                {
                    ThrowHelper.ThrowException("Content-Length is required but was not provided");
                }

                // buffer the entire contents for now
                var buffer = new byte[contentLength.Value];
                var responseContentStream = new MemoryStream(buffer);
                await response.Content.CopyToAsync(buffer).ConfigureAwait(false);
                responseContentStream.Position = 0;

                return new Tuple<IApiResponse, HttpRequest>(new HttpStreamResponse(response.StatusCode, responseContentStream.Length, response.GetContentEncoding(), responseContentStream, response.Headers), request);
            }
        }
    }
}
