// <copyright file="HttpClientRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientRequestFactory : IApiRequestFactory
    {
        private readonly HttpClient _client;
        private readonly HttpMessageHandler _handler;
        private readonly Uri _baseEndpoint;

        public HttpClientRequestFactory(Uri baseEndpoint, KeyValuePair<string, string>[] defaultHeaders, HttpMessageHandler handler = null, TimeSpan? timeout = null)
        {
            _handler = handler ?? new HttpClientHandler();
            _client = new HttpClient(_handler);
            _baseEndpoint = baseEndpoint;
            if (timeout.HasValue)
            {
                _client.Timeout = timeout.Value;
            }

            foreach (var pair in defaultHeaders)
            {
                _client.DefaultRequestHeaders.Add(pair.Key, pair.Value);
            }

            // Disable keep-alive
            _client.DefaultRequestHeaders.ConnectionClose = true;
        }

        public Uri GetEndpoint(string relativePath) => UriHelpers.Combine(_baseEndpoint, relativePath);

        public virtual string Info(Uri endpoint)
        {
            return endpoint.ToString();
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpClientRequest(_client, endpoint);
        }

        public void SetProxy(WebProxy proxy, NetworkCredential credential)
        {
            if (_handler is HttpClientHandler handler)
            {
                handler.Proxy = proxy;
                if (credential is not null)
                {
                    handler.Credentials = credential;
                }
            }
        }
    }
}
#endif
