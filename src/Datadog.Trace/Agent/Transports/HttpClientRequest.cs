// <copyright file="HttpClientRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientRequest : IApiRequest
    {
        private readonly HttpClient _client;
        private readonly HttpRequestMessage _request;

        public HttpClientRequest(HttpClient client, Uri endpoint)
        {
            _client = client;
            _request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
        }

        public async Task<IApiResponse> PostAsJsonAsync(IEvent events, JsonSerializer serializer)
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms, leaveOpen: true);
            using (var content = new StreamContent(ms))
            {
                using (JsonWriter writer = new JsonTextWriter(sw) { CloseOutput = true })
                {
                    serializer.Serialize(writer, events);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    _request.Content = content;
                    await writer.FlushAsync();
                    ms.Seek(0, SeekOrigin.Begin);
                    var response = await _client.SendAsync(_request).ConfigureAwait(false);
                    return new HttpClientResponse(response);
                }
            }
        }

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> traces)
        {
            // re-create HttpContent on every retry because some versions of HttpClient always dispose of it, so we can't reuse.
            using (var content = new ByteArrayContent(traces.Array, traces.Offset, traces.Count))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");
                _request.Content = content;

                var response = await _client.SendAsync(_request).ConfigureAwait(false);

                return new HttpClientResponse(response);
            }
        }
    }
}
#endif
