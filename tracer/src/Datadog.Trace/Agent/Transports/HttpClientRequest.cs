// <copyright file="HttpClientRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

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
