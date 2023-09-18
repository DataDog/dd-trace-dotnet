// <copyright file="ApiWebRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebRequest : IApiRequest, IMultipartApiRequest
    {
        private const string Boundary = "faa0a896-8bc8-48f3-b46d-016f2b15a884";
        private const string BoundarySeparator = "\r\n--" + Boundary + "\r\n";
        private const string BoundaryTrailer = "\r\n--" + Boundary + "--\r\n";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ApiWebRequest>();
        private readonly HttpWebRequest _request;

        private byte[] _boundarySeparatorInBytes;
        private byte[] _boundaryTrailerInBytes;

        public ApiWebRequest(HttpWebRequest request)
        {
            _request = request;
        }

        public bool UseGzip { get; set; }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
        }

        public async Task<IApiResponse> GetAsync()
        {
            ResetRequest(method: "GET", contentType: null, contentEncoding: null);

            try
            {
                var httpWebResponse = (HttpWebResponse)await _request.GetResponseAsync().ConfigureAwait(false);
                return new ApiWebResponse(httpWebResponse);
            }
            catch (WebException exception)
                when (exception.Status == WebExceptionStatus.ProtocolError && exception.Response != null)
            {
                // If the exception is caused by an error status code, swallow the exception and let the caller handle the result
                return new ApiWebResponse((HttpWebResponse)exception.Response);
            }
        }

        public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
            => PostAsync(bytes, contentType, null);

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
        {
            if (UseGzip)
            {
                ResetRequest(method: "POST", contentType, "gzip");
                using var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false);
                using var gzipStream = new GZipStream(requestStream, CompressionLevel.Fastest, true);
                await gzipStream.WriteAsync(bytes.Array, bytes.Offset, bytes.Count).ConfigureAwait(false);
                await gzipStream.FlushAsync().ConfigureAwait(false);
            }
            else
            {
                ResetRequest(method: "POST", contentType, contentEncoding);
                using var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false);
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

        /// <summary>
        /// Send a Post request using multipart form data.
        /// WARNING: Name and FileName of each MultipartFormItem instance must be ASCII encoding compatible.
        /// </summary>
        /// <param name="items">Multipart form data items</param>
        /// <returns>Task with the response</returns>
        public async Task<IApiResponse> PostAsync(params MultipartFormItem[] items)
        {
            if (items is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(items));
            }

            Log.Debug<int>("Sending multipart form request with {Count} items.", items.Length);

            ResetRequest(method: "POST", contentType: "multipart/form-data; boundary=" + Boundary, contentEncoding: null);

            using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                // Write form request using the boundary
                var boundaryBytes = _boundarySeparatorInBytes ??= Encoding.ASCII.GetBytes(BoundarySeparator);
                var trailerBytes = _boundaryTrailerInBytes ??= Encoding.ASCII.GetBytes(BoundaryTrailer);

                // Write each MultipartFormItem
                var itemsWritten = 0;
                foreach (var item in items)
                {
                    byte[] headerBytes = null;

                    // Check name is not null (required)
                    if (item.Name is null)
                    {
                        Log.Warning("Error encoding multipart form item name is null. Ignoring item");
                        continue;
                    }

                    // Ignore the item if the name contains ' or "
                    if (item.Name.IndexOf("\"", StringComparison.Ordinal) != -1 || item.Name.IndexOf("'", StringComparison.Ordinal) != -1)
                    {
                        Log.Warning("Error encoding multipart form item name: {Name}. Ignoring item.", item.Name);
                        continue;
                    }

                    // Do the same checks for FileName if not null
                    if (item.FileName is not null)
                    {
                        // Ignore the item if the name contains ' or "
                        if (item.FileName.IndexOf("\"", StringComparison.Ordinal) != -1 || item.FileName.IndexOf("'", StringComparison.Ordinal) != -1)
                        {
                            Log.Warning("Error encoding multipart form item filename: {FileName}. Ignoring item.", item.FileName);
                            continue;
                        }

                        headerBytes = Encoding.ASCII.GetBytes(
                            $"Content-Type: {item.ContentType}\r\nContent-Disposition: form-data; name=\"{item.Name}\"; filename=\"{item.FileName}\"\r\n\r\n");
                    }

                    headerBytes ??= Encoding.ASCII.GetBytes(
                        $"Content-Type: {item.ContentType}\r\nContent-Disposition: form-data; name=\"{item.Name}\"\r\n\r\n");

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

                if (itemsWritten > 0)
                {
                    await requestStream.WriteAsync(trailerBytes, 0, trailerBytes.Length).ConfigureAwait(false);
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
    }
}
