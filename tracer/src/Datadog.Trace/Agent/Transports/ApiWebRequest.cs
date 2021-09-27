// <copyright file="ApiWebRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebRequest : IApiRequest
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ApiWebRequest>();
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

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> traces, string contentType)
        {
            _request.Method = "POST";
            _request.ContentType = contentType;
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
                static Task WriteStream(Stream stream, JsonSerializer serializer, object events)
                {
                    var streamWriter = new StreamWriter(stream, System.Text.Encoding.UTF8, 1024, true);
                    using (var writer = new JsonTextWriter(streamWriter))
                    {
                        serializer.Serialize(writer, events);
                        return writer.FlushAsync();
                    }
                }

                await WriteStream(requestStream, serializer, events).ConfigureAwait(false);
                try
                {
                    var httpWebResponse = (HttpWebResponse)await _request.GetResponseAsync().ConfigureAwait(false);
                    var apiWebResponse = new ApiWebResponse(httpWebResponse);
                    if (httpWebResponse.StatusCode != HttpStatusCode.OK && httpWebResponse.StatusCode != HttpStatusCode.Accepted)
                    {
                        var sb = Util.StringBuilderCache.Acquire(0);
                        foreach (var item in _request.Headers)
                        {
                            sb.Append($"{item}: {_request.Headers[item.ToString()]} ");
                            sb.Append(", ");
                        }

                        using var ms = new MemoryStream();
                        await WriteStream(ms, serializer, events).ConfigureAwait(false);
                        ms.Position = 0;
                        using var sr = new StreamReader(ms);
                        Log.Warning("AppSec event not correctly sent to backend {statusCode} by class {className} with response {responseText}, request's headers were {headers}, request's payload was {payload}", new object[] { httpWebResponse.StatusCode, nameof(HttpStreamRequest), await apiWebResponse.ReadAsStringAsync().ConfigureAwait(false), Util.StringBuilderCache.GetStringAndRelease(sb), await sr.ReadToEndAsync().ConfigureAwait(false) });
                    }

                    return apiWebResponse;
                }
                catch (WebException exception)
                    when (exception.Status == WebExceptionStatus.ProtocolError && exception.Response != null)
                {
                    // If the exception is caused by an error status code, ignore it and let the caller handle the result
                    return new ApiWebResponse((HttpWebResponse)exception.Response);
                }
            }
        }
    }
}
