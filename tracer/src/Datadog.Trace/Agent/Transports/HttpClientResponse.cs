// <copyright file="HttpClientResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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

        public void Dispose()
        {
            _response.Dispose();
        }

        public string GetHeader(string headerName)
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

        public Task<string> ReadAsStringAsync()
        {
            return _response.Content.ReadAsStringAsync();
        }
    }
}
#endif
