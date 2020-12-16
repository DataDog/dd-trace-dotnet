using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamRequest : IApiRequest
    {
        private readonly Uri _uri;
        private readonly DatadogHttpClient _client;
        private readonly IStreamFactory _streamFactory;
        private readonly HttpHeaders _headers = new HttpHeaders();

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

        public async Task<IApiResponse> PostAsync(Span[][] traces, FormatterResolverWrapper formatterResolver)
        {
            using (var bidirectionalStream = _streamFactory.GetBidirectionalStream())
            {
                // buffer the entire contents for now so we can determine its size in bytes and avoid chunking.
                // TODO: support chunked transfer encoding to avoid buffering the entire contents
                var requestContentStream = new MemoryStream();
                await CachedSerializer.Instance.SerializeAsync(requestContentStream, traces, formatterResolver).ConfigureAwait(false);
                requestContentStream.Position = 0;

                var content = new StreamContent(requestContentStream, requestContentStream.Length);
                var request = new HttpRequest("POST", _uri.Host, _uri.PathAndQuery, _headers, content);

                // send request, get response
                var response = await _client.SendAsync(request, bidirectionalStream, bidirectionalStream).ConfigureAwait(false);

                // buffer the entire contents for now
                var responseContentStream = new MemoryStream();
                await response.Content.CopyToAsync(responseContentStream).ConfigureAwait(false);
                responseContentStream.Position = 0;

                var contentLength = response.ContentLength;
                if (contentLength != null && contentLength != responseContentStream.Length)
                {
                    throw new Exception("Content length from http headers does not match content's actual length.");
                }

                return new HttpStreamResponse(response.StatusCode, responseContentStream.Length, response.GetContentEncoding(), responseContentStream);
            }
        }
    }
}
