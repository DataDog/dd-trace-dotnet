// <copyright file="EfficientHttpClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NETCOREAPP || NETSTANDARD
    // We will use System.Net.Http.HttpClient (see comments below for a discussion).
    #define USE_HTTP_CLIENT
    #if NET5_0
        #define USE_HTTP_CLIENT_SEND_SYNC
    #endif
#else
    // We will use System.Net.WebRequest (see comments below for a discussion).
    // We do not need to set up a "#define USE_WEB_REQUEST", because the respective code path is configured via #if-USE_HTTP_CLIENT-#else-#endif.
    // #define USE_WEB_REQUEST
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Util;

#if USE_HTTP_CLIENT
using System.Net.Http;
#else
using System.Net;
#endif

namespace Datadog.Profiler
{
    /// <summary>
    /// We need an abstraction for HTTP client for several reasons:
    ///  - On .NET Framework:
    ///    we need to use WebRequest rather than HttpClient to avoid the dependency to System.Net.Http
    ///  - On .NET Core:
    ///    we need to use HttpClient rather than WebRequest because the dependency isn't an issue and WebRequest
    ///    on Net Core doesn't support TCP connection keep-alive until .NET 5.
    ///    (WebRequest on .NET Core is implemented on top of HttpClient. The way it works in older versions of Core
    ///    is that it creates a HttpClient under the hood, sends the request, and closes it. This closes the
    ///    underlying HTTP connection, so it means a new connection has to be established every time you send a request.
    ///    Since .NET 5 there is a pool of HttpClients (so the connection isn't closed).
    ///    It may sound like it isn't much, but it's made worse by the fact that (as of Jan 2021) the Datadog Agent only
    ///    binds the port to IPv4, and the client first tries to establish the connection with IPv6.
    ///    So, we need to wait for the IPv6 timeout before the connection is actually established.)
    ///  - We want a non-async send. This is because we operate from a non-thread-pooled dedicated background thread, and
    ///    we want to avoid interactions with the thread pool to avoid issues with thread starvation and other resource constraints.
    ///    HttpClient does not offer a sync API until .NET 5, so we perform a wait on the async API. But whereever available,
    ///    we use a sync API.
    ///
    /// @ToDo: Once we have a .NET 5 build target, use Send(..) intead of SendAsync(..) on the HttpClient.
    /// </summary>
    internal sealed class EfficientHttpClient : IDisposable
    {
#if USE_HTTP_CLIENT
        private readonly Action<MemoryStream> _releaseContentBufferStreamForReuseDelegate;
        private HttpClient _httpClient;

        // We reuse the memory stream that is used to buffer the request payload.
        // Our typical use case is non-async and non-concurrent. This re-use mechanism should be rubust in respect to being used concurrently,
        // but we do not focus on that case. The last released buffer always wins.
        private MemoryStream _reusableContentBufferStream = null;

        public EfficientHttpClient()
        {
            _httpClient = new HttpClient();
            _releaseContentBufferStreamForReuseDelegate = ReleaseContentBufferStreamForReuse;
        }

        public void Dispose()
        {
            HttpClient httpClient = Interlocked.Exchange(ref _httpClient, null);
            if (httpClient != null)
            {
                httpClient.Dispose();
            }
        }

        public EfficientHttpClient.MultipartFormPostRequest CreateNewMultipartFormPostRequest(string url)
        {
            HttpClient httpClient = _httpClient;
            if (httpClient == null)
            {
                throw new ObjectDisposedException($"This {nameof(EfficientHttpClient)} has been disposed.");
            }

            MemoryStream reusableContentBufferStream = Interlocked.Exchange(ref _reusableContentBufferStream, null);
            return new MultipartFormPostRequest(httpClient, url, reusableContentBufferStream, _releaseContentBufferStreamForReuseDelegate);
        }

        private void ReleaseContentBufferStreamForReuse(MemoryStream contentBufferStreamToReuse)
        {
            Interlocked.Exchange(ref _reusableContentBufferStream, contentBufferStreamToReuse);
        }
#else
        private static int _isGlobalInitPerformed = 0;

        public EfficientHttpClient()
        {
            int wasGlobalInitPerformed = Interlocked.Exchange(ref _isGlobalInitPerformed, 1);
            if (wasGlobalInitPerformed == 0)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
        }

        public void Dispose()
        {
        }

        public EfficientHttpClient.MultipartFormPostRequest CreateNewMultipartFormPostRequest(string url)
        {
            return new MultipartFormPostRequest(WebRequest.CreateHttp(url));
        }
#endif

        public struct Response
        {
            public Response(int statusCode, string statusCodeString, string payload, Exception error)
            {
                this.StatusCode = statusCode;
                this.StatusCodeString = statusCodeString;
                this.Payload = payload;
                this.Error = error;
            }

            public int StatusCode { get; }
            public string StatusCodeString { get; }
            public string Payload { get; }
            public Exception Error { get; }
        }

        public class MultipartFormPostRequest
        {
            private const string DocumentTextEncodingName = "utf-8";
            private static readonly Encoding BoundaryEncoding = Encoding.ASCII;
            private static readonly Encoding DocumentTextEncoding = Encoding.UTF8;

            private static readonly byte[] PlainTextContentTypeBytes = DocumentTextEncoding.GetBytes(
                                                $"Content-Type: text/plain; charset={DocumentTextEncodingName}\r\n\r\n");

            private static readonly byte[] PlainTextContentDispositionBytes1 = DocumentTextEncoding.GetBytes(
                                                "Content-Disposition: form-data; name=\"");

            private static readonly byte[] PlainTextContentDispositionBytes2 = DocumentTextEncoding.GetBytes(
                                                "\"\r\n");

            private static readonly byte[] OctetStreamContentTypeBytes = DocumentTextEncoding.GetBytes(
                                                "Content-Type: application/octet-stream\r\n\r\n");

            private static readonly byte[] OctetStreamContentDispositionBytes1 = DocumentTextEncoding.GetBytes(
                                                "Content-Disposition: form-data; name=\"");

            private static readonly byte[] OctetStreamContentDispositionBytes2 = DocumentTextEncoding.GetBytes(
                                                "\"; filename=\"");

            private static readonly byte[] OctetStreamContentDispositionBytes3 = DocumentTextEncoding.GetBytes(
                                                "\"\r\n");

#if USE_HTTP_CLIENT
            private readonly string _url;
            private readonly Action<MemoryStream> _releaseContentBufferStreamForReuseDelegate;
            private HttpClient _httpPoster;
#else
            private HttpWebRequest _httpPoster;
#endif
            private Stream _content;

            private List<KeyValuePair<string, string>> _customHeaders;

            private string _boundary;
            private byte[] _boundaryBytes;
            private byte[] _finalBoundaryBytes;

#if USE_HTTP_CLIENT
            public MultipartFormPostRequest(HttpClient httpPoster, string url, MemoryStream reuseableContent, Action<MemoryStream> releaseReuseableContent)
            {
                Validate.NotNull(httpPoster, nameof(httpPoster));
                Validate.NotNull(releaseReuseableContent, nameof(releaseReuseableContent));

                _httpPoster = httpPoster;
                _url = url;
                _releaseContentBufferStreamForReuseDelegate = releaseReuseableContent;

                if (reuseableContent != null)
                {
                    reuseableContent.Position = 0;
                    _content = reuseableContent;
                }
                else if (reuseableContent == null)
                {
                    _content = new MemoryStream();
                }

                InitHttpPosterAgnosticData();
            }
#else
            public MultipartFormPostRequest(HttpWebRequest httpPoster)
            {
                Validate.NotNull(httpPoster, nameof(httpPoster));

                _httpPoster = httpPoster;
                _httpPoster.Method = "POST";
                _httpPoster.KeepAlive = true;

                _content = _httpPoster.GetRequestStream();

                InitHttpPosterAgnosticData();
            }
#endif

            public void AddHeader(string name, string value)
            {
                Validate.NotNullOrWhitespace(name, nameof(name));
                Validate.NotNullOrWhitespace(value, nameof(value));
                _customHeaders.Add(new KeyValuePair<string, string>(name, value));
            }

            public void AddPlainTextFormPart(string name, string content)
            {
                Validate.NotNullOrWhitespace(name, nameof(name));
                Validate.NotNull(content, nameof(content));

                Write(_boundaryBytes);

                Write(PlainTextContentDispositionBytes1);
                Write(DocumentTextEncoding.GetBytes(name));
                Write(PlainTextContentDispositionBytes2);

                Write(PlainTextContentTypeBytes);

                Write(DocumentTextEncoding.GetBytes(content));
            }

            public Stream AddOctetStreamFormPart(string name, string filename)
            {
                Validate.NotNullOrWhitespace(name, nameof(name));
                Validate.NotNull(filename, nameof(filename));

                Write(_boundaryBytes);

                Write(OctetStreamContentDispositionBytes1);
                Write(DocumentTextEncoding.GetBytes(name));
                Write(OctetStreamContentDispositionBytes2);
                Write(DocumentTextEncoding.GetBytes(filename));
                Write(OctetStreamContentDispositionBytes3);

                Write(OctetStreamContentTypeBytes);

                WriteOnlyStream octetStream = new WriteOnlyStream(_content, leaveUnderlyingStreamOpenWhenDisposed: true);
                return octetStream;
            }

#if USE_HTTP_CLIENT
            public EfficientHttpClient.Response Send()
            {
                HttpClient httpClient = Interlocked.Exchange(ref _httpPoster, null);
                if (httpClient == null)
                {
                    throw new InvalidOperationException("This request has already been sent.");
                }

                Write(_finalBoundaryBytes);
                _content.Position = 0;

                int statusCode = 0;
                string statusCodeString = null;
                string payload = null;
                Exception error = null;

                MemoryStream contentBufferStream = null;
                if (_content is MemoryStream memStream)
                {
                    contentBufferStream = memStream;
                }

                HttpContent requestContent = (contentBufferStream != null && (contentBufferStream.Length < int.MaxValue - 1))
                                ? (HttpContent)new ByteArrayContent(contentBufferStream.GetBuffer(), 0, (int)_content.Length)
                                : (HttpContent)new StreamContent(_content);

                using (requestContent)
                {
                    requestContent.Headers.Add("Content-Type", $"multipart/form-data; boundary=\"{_boundary}\"");
                    requestContent.Headers.ContentLength = _content.Length;
                    for (int i = 0; i < _customHeaders.Count; i++)
                    {
                        KeyValuePair<string, string> headerInfo = _customHeaders[i];
                        requestContent.Headers.Add(headerInfo.Key, headerInfo.Value);
                    }

                    using (var request = new HttpRequestMessage(HttpMethod.Post, _url))
                    {
                        request.Content = requestContent;

                        try
                        {
#if USE_HTTP_CLIENT_SEND_SYNC
                            HttpResponseMessage response = httpClient.Send(request);
#else
                            HttpResponseMessage response = httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
#endif
                            using (response)
                            {
                                statusCode = (int)response.StatusCode;
                                statusCodeString = response.StatusCode.ToString();

#if USE_HTTP_CLIENT_SEND_SYNC
                                Stream payloadStream = response.Content.ReadAsStream();
#else
                                Stream payloadStream = response.Content.ReadAsStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();
#endif
                                using (payloadStream)
                                using (StreamReader payloadReader = new StreamReader(payloadStream))
                                {
                                    payload = payloadReader.ReadToEnd();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            error = ex;
                        }
                    }
                }

                // Return the allocated buffer for future reuse:
                if (contentBufferStream != null && _releaseContentBufferStreamForReuseDelegate != null)
                {
                    _content = null;
                    _releaseContentBufferStreamForReuseDelegate(contentBufferStream);
                }

                return new EfficientHttpClient.Response(statusCode, statusCodeString, payload, error);
            }
#else
            public EfficientHttpClient.Response Send()
            {
                HttpWebRequest httpPoster = Interlocked.Exchange(ref _httpPoster, null);
                if (httpPoster == null)
                {
                    throw new InvalidOperationException("This request has already been sent.");
                }

                Write(_finalBoundaryBytes);

                httpPoster.ContentType = $"multipart/form-data; boundary=\"{_boundary}\"";

                for (int i = 0; i < _customHeaders.Count; i++)
                {
                    KeyValuePair<string, string> headerInfo = _customHeaders[i];
                    httpPoster.Headers.Add(headerInfo.Key, headerInfo.Value);
                }

                int statusCode = 0;
                string statusCodeString = null;
                string payload = null;
                Exception error = null;

                try
                {
                    WebResponse response = httpPoster.GetResponse();

                    if (response != null && response is HttpWebResponse httpResponse)
                    {
                        statusCode = (int)httpResponse.StatusCode;
                        statusCodeString = httpResponse.StatusCode.ToString();
                    }

                    using (Stream payloadStream = response.GetResponseStream())
                    using (StreamReader payloadReader = new StreamReader(payloadStream))
                    {
                        payload = payloadReader.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                return new EfficientHttpClient.Response(statusCode, statusCodeString, payload, error);
            }
#endif
            private void InitHttpPosterAgnosticData()
            {
                _customHeaders = new List<KeyValuePair<string, string>>(capacity: 5);

                _boundary = Guid.NewGuid().ToString("N");
                _boundaryBytes = BoundaryEncoding.GetBytes($"\r\n--{_boundary}\r\n");
                _finalBoundaryBytes = BoundaryEncoding.GetBytes($"\r\n--{_boundary}--\r\n");
            }

            private void Write(byte[] bytes)
            {
                _content.Write(bytes, 0, bytes.Length);
            }
        } // class EfficientHttpClient.MultipartFormPostRequest
    }
}