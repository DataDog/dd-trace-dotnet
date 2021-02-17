using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamRequest : IApiRequest
    {
        /// <summary>
        /// This value is greater than any reasonable response we would receive from the agent.
        /// It is smaller than the internal default of 81920
        /// https://source.dot.net/#System.Private.CoreLib/Stream.cs,122
        /// It is a multiple of 4096.
        /// </summary>
        private const int ResponseReadBufferSize = 12_228;

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

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> traces)
        {
            using (var bidirectionalStream = _streamFactory.GetBidirectionalStream())
            {
                var content = new BufferContent(traces);
                var request = new HttpRequest("POST", _uri.Host, _uri.PathAndQuery, _headers, content);

                // send request, get response
                var response = await _client.SendAsync(request, bidirectionalStream, bidirectionalStream).ConfigureAwait(false);

                // Content-Length is required as we don't support chunked transfer
                var contentLength = response.Content.Length;
                if (!contentLength.HasValue)
                {
                    throw new Exception("Content-Length is required but was not provided");
                }

                // buffer the entire contents for now
                var buffer = new byte[contentLength.Value];
                var responseContentStream = new MemoryStream(buffer);
                await response.Content.CopyToAsync(buffer).ConfigureAwait(false);
                responseContentStream.Position = 0;

                return new HttpStreamResponse(response.StatusCode, responseContentStream.Length, response.GetContentEncoding(), responseContentStream);
            }
        }
    }
}
