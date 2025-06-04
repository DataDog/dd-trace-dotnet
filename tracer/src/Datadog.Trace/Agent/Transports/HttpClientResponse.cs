// <copyright file="HttpClientResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETCOREAPP
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientResponse : IApiResponse
    {
        private readonly HttpResponseMessage _response;

        public HttpClientResponse(HttpResponseMessage response)
        {
            _response = response;
        }

        public int StatusCode => (int)_response.StatusCode;

        public long ContentLength => _response.Content.Headers.ContentLength ?? -1;

        public string? ContentEncodingHeader => string.Join(',', _response.Content.Headers.ContentEncoding);

        public string? ContentTypeHeader => _response.Content.Headers.ContentType?.ToString();

        public ContentEncodingType GetContentEncodingType() =>
            _response.Content.Headers.ContentEncoding.Count switch
            {
                0 => ContentEncodingType.None,
                1 => ApiResponseExtensions.GetContentEncodingType(_response.Content.Headers.ContentEncoding.First()),
                _ => ContentEncodingType.Multiple,
            };

        public Encoding GetCharsetEncoding()
        {
            var charset = _response.Content.Headers.ContentType?.CharSet;
            if (string.IsNullOrEmpty(charset))
            {
                return EncodingHelpers.Utf8NoBom;
            }

            if (EncodingHelpers.TryGetWellKnownCharset(charset, out var wellKnown))
            {
                return wellKnown;
            }

            return EncodingHelpers.TryGetFromCharset(charset, out var parsed)
                       ? parsed
                       : EncodingHelpers.Utf8NoBom;
        }

        public void Dispose()
        {
            _response.Dispose();
        }

        public string? GetHeader(string headerName)
        {
            if (_response.Headers.TryGetValues(headerName, out var headers))
            {
                if (headers is string[] headersArray)
                {
                    if (headersArray.Length > 0)
                    {
                        return headersArray[0];
                    }
                }
                else
                {
                    return headers.FirstOrDefault();
                }
            }

            return null;
        }

        public Task<Stream> GetStreamAsync()
        {
            return _response.Content.ReadAsStreamAsync();
        }
    }
}
#endif
