// <copyright file="ApiWebRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebRequest : IApiRequest
    {
        private readonly HttpWebRequest _request;

        public ApiWebRequest(HttpWebRequest request)
        {
            _request = request;

            // Default headers
            foreach (var pair in AgentHttpHeaderNames.DefaultHeaders)
            {
                _request.Headers.Add(pair.Key, pair.Value);
            }
        }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
        }

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> traces)
        {
            _request.Method = "POST";
            _request.ContentType = "application/msgpack";
            using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                await requestStream.WriteAsync(traces.Array, traces.Offset, traces.Count).ConfigureAwait(false);
            }

            try
            {
                var httpWebResponse = (HttpWebResponse)await _request.GetResponseAsync().ConfigureAwait(false);
                return new ApiWebResponse(httpWebResponse);
            }
            catch (WebException exception)
                when (exception.Status == WebExceptionStatus.ProtocolError && exception.Response != null)
            {
                // If the exception is caused by an error status code, ignore it and let the caller handle the result
                return new ApiWebResponse((HttpWebResponse)exception.Response);
            }
        }

        public async Task<IApiResponse> PostAsJsonAsync(IEvent events, JsonSerializer serializer)
        {
            _request.Method = "POST";
            _request.ContentType = "application/json";

            using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                using (var writer = new JsonTextWriter(new StreamWriter(requestStream)))
                {
                    serializer.Serialize(writer, events);
                    await writer.FlushAsync();
                }
            }

            try
            {
                var httpWebResponse = (HttpWebResponse)await _request.GetResponseAsync().ConfigureAwait(false);
                return new ApiWebResponse(httpWebResponse);
            }
            catch (WebException exception)
                when (exception.Status == WebExceptionStatus.ProtocolError && exception.Response != null)
            {
                // If the exception is caused by an error status code, ignore it and let the caller handle the result
                return new ApiWebResponse((HttpWebResponse)exception.Response);
            }
        }

        public async Task<string> RequestContent()
        {
            var sb = new StringBuilder();
            foreach (var item in _request.Headers)
            {
                sb.Append(item.ToString());
                sb.Append(", ");
            }

            // we cant seem to be able re read the request stream once response has been read so just return headers

            return $"Headers: {sb}. With {nameof(ApiWebRequest)}, request's payload can't be displayed for now.{(await _request.GetRequestStreamAsync())}";
        }
    }
}
