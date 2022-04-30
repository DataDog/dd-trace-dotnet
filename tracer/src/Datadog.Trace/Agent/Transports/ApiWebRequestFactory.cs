// <copyright file="ApiWebRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using Datadog.Trace.Util;

#pragma warning disable CS0618 // WebRequest, HttpWebRequest, ServicePoint, and WebClient are obsolete. Use HttpClient instead.

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebRequestFactory : IApiRequestFactory
    {
        private readonly KeyValuePair<string, string>[] _defaultHeaders;
        private readonly Uri _baseEndpoint;
        private WebProxy _proxy;
        private NetworkCredential _credential;
        private TimeSpan? _timeout;

        public ApiWebRequestFactory(Uri baseEndpoint, KeyValuePair<string, string>[] defaultHeaders, TimeSpan? timeout = null)
        {
            _baseEndpoint = baseEndpoint;
            _defaultHeaders = defaultHeaders;
            _timeout = timeout;
        }

        public string Info(Uri endpoint)
        {
            return endpoint.ToString();
        }

        public Uri GetEndpoint(string relativePath) => UriHelpers.Combine(_baseEndpoint, relativePath);

        public IApiRequest Create(Uri endpoint)
        {
            var request = WebRequest.CreateHttp(endpoint);
            if (_proxy is not null)
            {
                request.Proxy = _proxy;
            }

            if (_credential is not null)
            {
                request.Credentials = _credential;
            }

            if (_timeout.HasValue)
            {
                request.Timeout = (int)_timeout.Value.TotalMilliseconds;
            }

            foreach (var pair in _defaultHeaders)
            {
                request.Headers.Add(pair.Key, pair.Value);
            }

            return new ApiWebRequest(request);
        }

        public void SetProxy(WebProxy proxy, NetworkCredential credential)
        {
            _proxy = proxy;
            _credential = credential;
        }
    }
}
