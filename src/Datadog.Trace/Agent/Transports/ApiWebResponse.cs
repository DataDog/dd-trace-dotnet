// <copyright file="ApiWebResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebResponse : IApiResponse, IDisposable
    {
        private readonly HttpWebResponse _response;

        public ApiWebResponse(HttpWebResponse response)
        {
            _response = response;
        }

        public int StatusCode => (int)_response.StatusCode;

        public long ContentLength => _response.ContentLength;

        public string GetHeader(string headerName) => _response.Headers[headerName];

        public async Task<string> ReadAsStringAsync()
        {
            using (var responseStream = _response.GetResponseStream())
            {
                var reader = new StreamReader(responseStream);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _response?.Dispose();
        }
    }
}
