// <copyright file="ApiWebRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebRequest : IApiRequest, IMultipartApiRequest
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ApiWebRequest>();
        private readonly HttpWebRequest _request;

        public ApiWebRequest(HttpWebRequest request)
        {
            _request = request;
        }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
        }

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
        {
            _request.Method = "POST";
            _request.ContentType = contentType;
            using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                await requestStream.WriteAsync(bytes.Array, bytes.Offset, bytes.Count).ConfigureAwait(false);
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

        public async Task<IApiResponse> PostAsync(params MultipartFormItem[] items)
        {
            Log.Information<int>("Sending multipart form request with {Count} items.", items?.Length ?? 0);

            var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");

            _request.Method = "POST";
            _request.ContentType = "multipart/form-data; boundary=" + boundary;

            using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                var boundaryBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

                foreach (var item in items)
                {
                    await requestStream.WriteAsync(boundaryBytes, 0, boundaryBytes.Length).ConfigureAwait(false);
                    byte[] headerBytes;
                    if (item.FileName is null)
                    {
                        headerBytes = Encoding.UTF8.GetBytes(
                            $"Content-Disposition: form-data; name=\"{item.Name}\"\r\nContent-Type: {item.ContentType}\r\n\r\n");
                    }
                    else
                    {
                        headerBytes = Encoding.UTF8.GetBytes(
                            $"Content-Disposition: form-data; name=\"{item.Name}\"; filename=\"{item.FileName}\"\r\nContent-Type: {item.ContentType}\r\n\r\n");
                    }

                    await requestStream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
                    if (item.ContentInBytes is { } arraySegment)
                    {
                        Log.Information("Adding to Multipart Byte Array | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                        await requestStream.WriteAsync(arraySegment.Array, arraySegment.Offset, arraySegment.Count).ConfigureAwait(false);
                    }
                    else if (item.ContentInStream is { } stream)
                    {
                        Log.Information("Adding to Multipart Stream | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                        await stream.CopyToAsync(requestStream).ConfigureAwait(false);
                    }
                }

                var trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                await requestStream.WriteAsync(trailer, 0, trailer.Length).ConfigureAwait(false);
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
    }
}
