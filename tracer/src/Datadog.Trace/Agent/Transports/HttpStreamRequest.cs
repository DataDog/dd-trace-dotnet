// <copyright file="HttpStreamRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

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

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpStreamRequest>();
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

        public async Task<IApiResponse> PostAsJsonAsync(IEvent events, JsonSerializer serializer)
        {
            var memoryStream = new MemoryStream();
            var sw = new StreamWriter(memoryStream);
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, events);
                await writer.FlushAsync().ConfigureAwait(false);
                await memoryStream.FlushAsync().ConfigureAwait(false);
                memoryStream.Seek(0, SeekOrigin.Begin);
                var buffer = memoryStream.GetBuffer();
                var result = await PostSegmentAsync(new ArraySegment<byte>(buffer, 0, (int)memoryStream.Length), "application/json").ConfigureAwait(false);
                var response = result.Item1;
                var request = result.Item2;
                if (response.StatusCode != 200 && response.StatusCode != 202)
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    using var sr = new StreamReader(memoryStream);
                    var headers = string.Join(", ", request.Headers.Select(h => $"{h.Name}: {h.Value}"));
                    var payload = await sr.ReadToEndAsync().ConfigureAwait(false);

                    Log.Warning("AppSec event not correctly sent to backend {statusCode} by class {className} with response {responseText}, request headers: were {headers}, payload was: {payload}", new object[] { response.StatusCode, nameof(HttpStreamRequest), await response.ReadAsStringAsync().ConfigureAwait(false), headers, payload });
                }

                return response;
            }
        }

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> traces, string contentType) => (await PostSegmentAsync(traces, contentType).ConfigureAwait(false)).Item1;

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
