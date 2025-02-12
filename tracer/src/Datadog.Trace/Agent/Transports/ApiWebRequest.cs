// <copyright file="ApiWebRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using static Datadog.Trace.HttpOverStreams.DatadogHttpValues;

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebRequest : IApiRequest
    {
        private const string BoundarySeparator = $"{CrLf}--{Boundary}{CrLf}";
        private const string BoundaryTrailer = $"{CrLf}--{Boundary}--{CrLf}";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ApiWebRequest>();
        private readonly HttpWebRequest _request;

        private byte[] _boundarySeparatorInBytes;
        private byte[] _boundaryTrailerInBytes;

        public ApiWebRequest(HttpWebRequest request)
        {
            _request = request;
        }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
        }

        public Task<IApiResponse> GetAsync()
        {
            ResetRequest(method: "GET", contentType: null, contentEncoding: null);

            return FinishAndGetResponse();
        }

        public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
            => PostAsync(bytes, contentType, null);

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
        {
            ResetRequest(method: "POST", contentType, contentEncoding);

            using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                await requestStream.WriteAsync(bytes.Array, bytes.Offset, bytes.Count).ConfigureAwait(false);
            }

            return await FinishAndGetResponse().ConfigureAwait(false);
        }

        public async Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
        {
            ResetRequest(method: "POST", ContentTypeHelper.GetContentType(contentType, multipartBoundary), contentEncoding);

            using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                await writeToRequestStream(requestStream).ConfigureAwait(false);
            }

            return await FinishAndGetResponse().ConfigureAwait(false);
        }

        /// <summary>
        /// Send a Post request using multipart form data.
        /// WARNING: Name and FileName of each MultipartFormItem instance must be ASCII encoding compatible.
        /// </summary>
        /// <param name="items">Multipart form data items</param>
        /// <param name="multipartCompression">Multipart compression</param>
        /// <returns>Task with the response</returns>
        public async Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
        {
            if (items is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(items));
            }

            Log.Debug<int>("Sending multipart form request with {Count} items.", items.Length);

            ResetRequest(method: "POST", contentType: "multipart/form-data; boundary=" + Boundary, contentEncoding: multipartCompression == MultipartCompression.GZip ? "gzip" : null);
            using (var reqStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                if (multipartCompression == MultipartCompression.GZip)
                {
                    Log.Debug("Using MultipartCompression.GZip");
                    using var gzip = new GZipStream(reqStream, CompressionMode.Compress, leaveOpen: true);
                    await WriteToStreamAsync(items, gzip).ConfigureAwait(false);
                    await gzip.FlushAsync().ConfigureAwait(false);
                    Log.Debug("Compressing multipart payload...");
                }
                else
                {
                    await WriteToStreamAsync(items, reqStream).ConfigureAwait(false);
                }
            }

            return await FinishAndGetResponse().ConfigureAwait(false);

            async Task WriteToStreamAsync(MultipartFormItem[] multipartItems, Stream requestStream)
            {
                // Write form request using the boundary
                var boundaryBytes = _boundarySeparatorInBytes ??= Encoding.ASCII.GetBytes(BoundarySeparator);
                var trailerBytes = _boundaryTrailerInBytes ??= Encoding.ASCII.GetBytes(BoundaryTrailer);

                // Write each MultipartFormItem
                var itemsWritten = 0;
                foreach (var item in multipartItems)
                {
                    if (!item.IsValid(Log))
                    {
                        continue;
                    }

                    var headerBytes = Encoding.ASCII.GetBytes(
                        item.FileName is not null
                            ? $"Content-Type: {item.ContentType}\r\nContent-Disposition: form-data; name=\"{item.Name}\"; filename=\"{item.FileName}\"\r\n\r\n"
                            : $"Content-Type: {item.ContentType}\r\nContent-Disposition: form-data; name=\"{item.Name}\"\r\n\r\n");

                    if (itemsWritten == 0)
                    {
                        // If we are writing the first item, we skip the initial `\r\n` in the array
                        await requestStream.WriteAsync(boundaryBytes, 2, boundaryBytes.Length - 2).ConfigureAwait(false);
                    }
                    else
                    {
                        await requestStream.WriteAsync(boundaryBytes, 0, boundaryBytes.Length).ConfigureAwait(false);
                    }

                    await requestStream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
                    if (item.ContentInBytes is { } arraySegment)
                    {
                        Log.Debug("Adding to Multipart Byte Array | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                        await requestStream.WriteAsync(arraySegment.Array, arraySegment.Offset, arraySegment.Count).ConfigureAwait(false);
                    }
                    else if (item.ContentInStream is { } stream)
                    {
                        Log.Debug("Adding to Multipart Stream | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                        await stream.CopyToAsync(requestStream).ConfigureAwait(false);
                    }

                    itemsWritten++;
                }

                if (itemsWritten == 0)
                {
                    await requestStream.WriteAsync(boundaryBytes, 2, boundaryBytes.Length - 2).ConfigureAwait(false);
                }

                await requestStream.WriteAsync(trailerBytes, 0, trailerBytes.Length).ConfigureAwait(false);
            }
        }

        private void ResetRequest(string method, string contentType, string contentEncoding)
        {
            _request.Method = method;
            _request.ContentType = string.IsNullOrEmpty(contentType) ? null : contentType;
            if (string.IsNullOrEmpty(contentEncoding))
            {
                _request.Headers.Remove(HttpRequestHeader.ContentEncoding);
            }
            else
            {
                _request.Headers.Set(HttpRequestHeader.ContentEncoding, contentEncoding);
            }
        }

        private async Task<IApiResponse> FinishAndGetResponse()
        {
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
