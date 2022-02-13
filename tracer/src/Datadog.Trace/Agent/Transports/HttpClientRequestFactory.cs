// <copyright file="HttpClientRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientRequestFactory : IApiRequestFactory
    {
        private readonly HttpClient _client;

        public HttpClientRequestFactory(KeyValuePair<string, string>[] defaultHeaders, HttpMessageHandler handler = null)
        {
            _client = handler == null ? new HttpClient() : new HttpClient(handler);

            foreach (var pair in defaultHeaders)
            {
                _client.DefaultRequestHeaders.Add(pair.Key, pair.Value);
            }
        }

        public string Info(Uri endpoint)
        {
            return endpoint.ToString();
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpClientRequest(_client, endpoint);
        }
    }
}
#endif
