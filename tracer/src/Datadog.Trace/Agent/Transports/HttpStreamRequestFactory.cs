// <copyright file="HttpStreamRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Util;

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

        public Uri GetEndpoint(string relativePath)
        {
            // HttpStreamRequest doesn't actually use the endpoint URI to route the request,
            // but it does add the Host header to the request. For non TCP workloads this can cause issues
            // as we include a file URI in the Host header e.g. see this issue around nodejs (called from go)
            // https://github.com/grpc/grpc-go/issues/2628 and issue/discussion in aspnetcore here:
            // https://github.com/dotnet/aspnetcore/issues/18522.
            // To play it safe, use localhost as the host instead of the UDS socket name/ named pipe
            return UriHelpers.Combine(new Uri("http://localhost"), relativePath);
        }

        public string Info(Uri endpoint)
        {
            return $"{_streamFactory.Info()} to {endpoint}";
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpStreamRequest(_httpClient, endpoint, _streamFactory);
        }

        public void SetProxy(WebProxy proxy, NetworkCredential credential)
        {
        }
    }
}
