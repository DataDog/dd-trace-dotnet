// <copyright file="HttpClientRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientRequest : IApiRequest
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpClientRequest>();

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
            var memoryStream = new MemoryStream();
            var sw = new StreamWriter(memoryStream, leaveOpen: true);
            using (var content = new StreamContent(memoryStream))
            {
                using (JsonWriter writer = new JsonTextWriter(sw) { CloseOutput = true })
                {
                    serializer.Serialize(writer, events);
                    content.Headers.ContentType = new MediaTypeHeaderValue(MimeTypes.Json);
                    _request.Content = content;
                    await writer.FlushAsync().ConfigureAwait(false);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var response = new HttpClientResponse(await _client.SendAsync(_request).ConfigureAwait(false));
                    if (response.StatusCode != 200 && response.StatusCode != 202)
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        using var sr = new StreamReader(memoryStream);
                        var headers = string.Join(", ", _request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));

                        var payload = await sr.ReadToEndAsync().ConfigureAwait(false);
                        Log.Warning("AppSec event not correctly sent to backend {statusCode} by class {className} with response {responseText}, request headers: were {headers}, payload was: {payload}", new object[] { response.StatusCode, nameof(HttpClientRequest), await response.ReadAsStringAsync().ConfigureAwait(false), headers, payload });
                    }

                    return response;
                }
            }
        }

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
        {
            // re-create HttpContent on every retry because some versions of HttpClient always dispose of it, so we can't reuse.
            using (var content = new ByteArrayContent(bytes.Array, bytes.Offset, bytes.Count))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                _request.Content = content;

                var response = await _client.SendAsync(_request).ConfigureAwait(false);

                return new HttpClientResponse(response);
            }
        }
    }
}
#endif
