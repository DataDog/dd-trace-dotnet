// <copyright file="HttpStreamRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpStreamRequestFactory : IApiRequestFactory
    {
        private readonly IStreamFactory _streamFactory;
        private readonly DatadogHttpClient _httpClient;

        public HttpStreamRequestFactory(IStreamFactory streamFactory, DatadogHttpClient httpClient)
        {
            _streamFactory = streamFactory;
            _httpClient = httpClient;
        }

        public string Info(Uri endpoint)
        {
            return $"{_streamFactory.Info()} to {endpoint}";
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpStreamRequest(_httpClient, endpoint, _streamFactory);
        }
    }
}
