// <copyright file="HttpStreamRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
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

        public async Task<IApiResponse> GetAsync()
            => (await SendAsync(WebRequestMethods.Http.Get, null, null, null, chunkedEncoding: false).ConfigureAwait(false)).Item1;

        public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
            => PostAsync(bytes, contentType, contentEncoding: null);

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
            => (await SendAsync(WebRequestMethods.Http.Post, contentType, new BufferContent(bytes), contentEncoding, chunkedEncoding: false).ConfigureAwait(false)).Item1;

        public async Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
            => (await SendAsync(WebRequestMethods.Http.Post, contentType, new HttpOverStreams.HttpContent.PushStreamContent(writeToRequestStream), contentEncoding, chunkedEncoding: true, multipartBoundary).ConfigureAwait(false)).Item1;

        public async Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
        {
            var contentEncoding = multipartCompression switch
            {
                MultipartCompression.None => null,
                MultipartCompression.GZip => "gzip",
                _ => throw new InvalidOperationException($"Unknown compression type: {multipartCompression}"),
            };

            var sendResult = await SendAsync(
                                    WebRequestMethods.Http.Post,
                                    contentType: MimeTypes.MultipartFormData,
                                    new HttpOverStreams.HttpContent.MultipartFormContent(items, multipartCompression),
                                    contentEncoding: contentEncoding,
                                    chunkedEncoding: true,
                                    DatadogHttpValues.Boundary)
                               .ConfigureAwait(false);

            return sendResult.Item1;
        }

        private async Task<Tuple<IApiResponse, HttpRequest>> SendAsync(string verb, string contentType, IHttpContent content, string contentEncoding, bool chunkedEncoding, string multipartBoundary = null)
        {
            using (var bidirectionalStream = _streamFactory.GetBidirectionalStream())
            {
                if (contentType != null)
                {
                    _headers.Add("Content-Type", ContentTypeHelper.GetContentType(contentType, multipartBoundary));
                }

                if (!string.IsNullOrEmpty(contentEncoding))
                {
                    _headers.Add("Content-Encoding", contentEncoding);
                }

                if (chunkedEncoding)
                {
                    _headers.Add("Transfer-Encoding", "chunked");
                }

                var request = new HttpRequest(verb, _uri.Host, _uri.PathAndQuery, _headers, content);
                // send request, get response
                var response = await _client.SendAsync(request, bidirectionalStream, bidirectionalStream).ConfigureAwait(false);

                MemoryStream responseContentStream;
                if (response.ContentLength is { } contentLength)
                {
                    // buffer the entire contents for now
                    var buffer = new byte[contentLength];
                    responseContentStream = new MemoryStream(buffer);
                    await response.Content.CopyToAsync(buffer).ConfigureAwait(false);
                }
                else
                {
                    // We don't know the length, so can't use a fixed size buffer.
                    // This happens when we receive chunked responses, so is relatively rare.
                    // TODO: We should look at removing this buffering, but it requires a big refactor
                    responseContentStream = new MemoryStream();
                    await response.Content.CopyToAsync(responseContentStream).ConfigureAwait(false);
                }

                responseContentStream.Position = 0;

                return new Tuple<IApiResponse, HttpRequest>(new HttpStreamResponse(response.StatusCode, responseContentStream.Length, response.GetContentEncoding(), responseContentStream, response.Headers), request);
            }
        }
    }
}
