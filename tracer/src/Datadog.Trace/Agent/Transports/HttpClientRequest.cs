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
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientRequest : IApiRequest, IMultipartApiRequest
    {
        private const string Boundary = "faa0a896-8bc8-48f3-b46d-016f2b15a884";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpClientRequest>();

        private readonly HttpClient _client;
        private readonly HttpRequestMessage _postRequest;
        private readonly HttpRequestMessage _getRequest;
        private readonly Uri _uri;

        public HttpClientRequest(HttpClient client, Uri endpoint)
        {
            _client = client;
            _postRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            _getRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
            _uri = endpoint;
        }

        public void AddHeader(string name, string value)
        {
            _postRequest.Headers.Add(name, value);
            _getRequest.Headers.Add(name, value);
        }

        public async Task<IApiResponse> GetAsync()
        {
            _getRequest.Content = null;

            return new HttpClientResponse(await _client.SendAsync(_getRequest).ConfigureAwait(false));
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
                    _postRequest.Content = content;
                    await writer.FlushAsync().ConfigureAwait(false);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var response = new HttpClientResponse(await _client.SendAsync(_postRequest).ConfigureAwait(false));
                    if (response.StatusCode != 200 && response.StatusCode != 202)
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        using var sr = new StreamReader(memoryStream);
                        var headers = string.Join(", ", _postRequest.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));

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
                _postRequest.Content = content;

                var response = await _client.SendAsync(_postRequest).ConfigureAwait(false);

                return new HttpClientResponse(response);
            }
        }

        public async Task<IApiResponse> PostAsync(params MultipartFormItem[] items)
        {
            if (items is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(items));
            }

            Log.Debug<int>("Sending multipart form request with {Count} items.", items.Length);

            using var formDataContent = new MultipartFormDataContent(boundary: Boundary);
            _postRequest.Content = formDataContent;

            foreach (var item in items)
            {
                HttpContent content = null;

                // Adds a form data item
                if (item.ContentInBytes is { } arraySegment)
                {
                    content = new ByteArrayContent(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
                    Log.Debug("Adding to Multipart Byte Array | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                }
                else if (item.ContentInStream is { } stream)
                {
                    content = new StreamContent(stream);
                    Log.Debug("Adding to Multipart Stream | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                }
                else
                {
                    continue;
                }

                content.Headers.ContentType = new MediaTypeHeaderValue(item.ContentType);
                if (item.FileName is not null)
                {
                    formDataContent.Add(content, item.Name, item.FileName);
                }
                else
                {
                    formDataContent.Add(content, item.Name);
                }
            }

            var response = await _client.SendAsync(_postRequest).ConfigureAwait(false);
            return new HttpClientResponse(response);
        }
    }
}
#endif
