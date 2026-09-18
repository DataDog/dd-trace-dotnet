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
#if NET5_0_OR_GREATER // in .NET 6 we derive a SocketHandlerRequestFactory
    internal class HttpClientRequestFactory : IApiRequestFactory
#else
    internal sealed class HttpClientRequestFactory : IApiRequestFactory
#endif
    {
        private readonly HttpClient _client;
        private readonly HttpMessageHandler _handler;
        private readonly HttpClientHandler _apiKeyProtectedHandler;
        private readonly bool _disableProxyForPlaintextLoopback;
        private readonly bool _hasApiKeyHeader;
        private readonly Uri _baseEndpoint;

        public HttpClientRequestFactory(
            Uri baseEndpoint,
            KeyValuePair<string, string>[] defaultHeaders,
            HttpMessageHandler handler = null,
            TimeSpan? timeout = null,
            DecompressionMethods automaticDecompression = DecompressionMethods.None)
        {
            _baseEndpoint = baseEndpoint;
            foreach (var pair in defaultHeaders)
            {
                if (string.Equals(pair.Key, ApiKeyHttpTransportGuard.ApiKeyHeaderName, StringComparison.OrdinalIgnoreCase))
                {
                    _hasApiKeyHeader = true;
                }
            }

            if (_hasApiKeyHeader && handler is not null)
            {
                throw new ApiKeyHttpTransportException("Caller-provided HTTP handlers are not supported for protected DD-API-KEY transport.");
            }

            _handler = handler ?? new HttpClientHandler { AutomaticDecompression = automaticDecompression };
            _disableProxyForPlaintextLoopback = _hasApiKeyHeader && ApiKeyHttpTransportGuard.IsPlaintextLoopback(baseEndpoint);
            if (_hasApiKeyHeader)
            {
                _apiKeyProtectedHandler = (HttpClientHandler)_handler;
                _apiKeyProtectedHandler.AllowAutoRedirect = false;
                if (_disableProxyForPlaintextLoopback)
                {
                    _apiKeyProtectedHandler.UseProxy = false;
                }
            }

            _client = new HttpClient(_handler);
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

        public Uri GetEndpoint(string relativePath) => relativePath is null ? _baseEndpoint : UriHelpers.Combine(_baseEndpoint, relativePath);

#if NET5_0_OR_GREATER // in .NET 6 we derive a SocketHandlerRequestFactory
        public virtual string Info(Uri endpoint)
#else
        public string Info(Uri endpoint)
#endif
        {
            return endpoint.ToString();
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpClientRequest(_client, _apiKeyProtectedHandler, endpoint);
        }

        public void SetProxy(WebProxy proxy, NetworkCredential credential)
        {
            if (_disableProxyForPlaintextLoopback)
            {
                return;
            }

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
