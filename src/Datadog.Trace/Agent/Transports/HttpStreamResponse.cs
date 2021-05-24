// <copyright file="HttpStreamResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamResponse : IApiResponse
    {
        private readonly HttpHeaders _headers;
        private string _responseCache;

        public HttpStreamResponse(int statusCode, long contentLength, Encoding encoding, Stream responseStream, HttpHeaders headers)
        {
            StatusCode = statusCode;
            ContentLength = contentLength;
            Encoding = encoding;
            ResponseStream = responseStream;
            _headers = headers;
        }

        public int StatusCode { get; }

        public long ContentLength { get; }

        public Encoding Encoding { get; }

        public Stream ResponseStream { get; }

        public void Dispose()
        {
        }

        public string GetHeader(string headerName) => _headers.GetValue(headerName);

        public async Task<string> ReadAsStringAsync()
        {
            if (_responseCache == null)
            {
                using (var reader = new StreamReader(ResponseStream, Encoding, detectEncodingFromByteOrderMarks: false, (int)ContentLength, leaveOpen: true))
                {
                    _responseCache = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            return _responseCache;
        }
    }
}
