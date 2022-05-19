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
        private readonly Uri _baseEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpStreamRequestFactory"/> class.
        /// </summary>
        /// <param name="streamFactory">The <see cref="IStreamFactory"/> to use when creating <see cref="HttpStreamRequest"/></param>
        /// <param name="httpClient">The <see cref="DatadogHttpClient"/> to use to associate with the <see cref="HttpStreamRequest"/></param>
        /// <param name="baseEndpoint"> The base endpoint to use when constructing an <see cref="HttpRequest"/> in <see cref="GetEndpoint"/>..
        /// The endpoint returned from <see cref="GetEndpoint"/>, isn't actually used to route the request,
        /// but it is used to construct the HTTP message, by using the the Host header and the path.
        /// For non TCP workloads, failure to include a valid Host header (e.g. using a file URI) can cause issues
        /// e.g. see this issue around nodejs (called from go) https://github.com/grpc/grpc-go/issues/2628 and
        /// issue/discussion in aspnetcore here https://github.com/dotnet/aspnetcore/issues/18522.
        /// Typically, you should use <c>http://localhost</c> as the host instead for non-TCP clients (e.g. UDS and named pipes)</param>
        public HttpStreamRequestFactory(IStreamFactory streamFactory, DatadogHttpClient httpClient, Uri baseEndpoint)
        {
            _streamFactory = streamFactory;
            _httpClient = httpClient;
            _baseEndpoint = baseEndpoint;
        }

        public Uri GetEndpoint(string relativePath)
        {
            return UriHelpers.Combine(_baseEndpoint, relativePath);
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
