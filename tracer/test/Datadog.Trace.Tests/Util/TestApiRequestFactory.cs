// <copyright file="TestApiRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tests.Util;

internal class TestApiRequestFactory : IApiRequestFactory
{
    private readonly Uri _baseEndpoint;
    private readonly Func<Uri, TestApiRequest>[] _requestsToSend;

    public TestApiRequestFactory(Uri baseEndpoint, params Func<Uri, TestApiRequest>[] requestsToSend)
    {
        _baseEndpoint = baseEndpoint;
        _requestsToSend = requestsToSend;
    }

    public List<TestApiRequest> RequestsSent { get; } = new();

    public Uri GetEndpoint(string relativePath)
    {
        return UriHelpers.Combine(_baseEndpoint, relativePath);
    }

    public string Info(Uri endpoint) => endpoint.ToString();

    public IApiRequest Create(Uri endpoint)
    {
        var request = (_requestsToSend is null || RequestsSent.Count >= _requestsToSend.Length)
                          ? new TestApiRequest(endpoint)
                          : _requestsToSend[RequestsSent.Count](endpoint);

        RequestsSent.Add(request);

        return request;
    }

    public void SetProxy(WebProxy proxy, NetworkCredential credential)
    {
    }

    internal class TestApiRequest : IApiRequest
    {
        private readonly Func<TestApiRequest, string, TestApiResponse> _createResponseFunc;

        public TestApiRequest(Uri endpoint)
            : this(endpoint, statusCode: 200)
        {
        }

        public TestApiRequest(Uri endpoint, int statusCode)
            : this(endpoint, (_, contentType) => new TestApiResponse(statusCode, "The message body", contentType))
        {
        }

        public TestApiRequest(Uri endpoint, Func<TestApiRequest, string, TestApiResponse> createResponseFunc)
        {
            Endpoint = endpoint;
            _createResponseFunc = createResponseFunc;
        }

        public Uri Endpoint { get; }

        public Dictionary<string, string> ExtraHeaders { get; } = new();

        public List<TestApiResponse> Responses { get; } = new();

        public Task<IApiResponse> GetAsync()
        {
            throw new NotImplementedException();
        }

        public void AddHeader(string name, string value)
        {
            ExtraHeaders.Add(name, value);
        }

        public Task<IApiResponse> PostAsync(ArraySegment<byte> traces, string contentType)
        {
            var response = _createResponseFunc(this, contentType);
            Responses.Add(response);

            return Task.FromResult((IApiResponse)response);
        }
    }

    internal class FaultyApiRequest : TestApiRequest
    {
        public FaultyApiRequest(Uri endpoint, int statusCode = 500)
            : base(endpoint, statusCode)
        {
        }
    }

    internal class TestApiResponse : IApiResponse
    {
        private readonly string _body;

        public TestApiResponse(int statusCode, string body, string contentType, Dictionary<string, string> headers = null)
        {
            StatusCode = statusCode;
            _body = body;
            ContentType = contentType;
            Headers = headers ?? new Dictionary<string, string>();
        }

        public string ContentType { get; }

        public Dictionary<string, string> Headers { get; }

        public int StatusCode { get; }

        public long ContentLength => _body?.Length ?? 0;

        public void Dispose()
        {
        }

        public string GetHeader(string headerName)
            => Headers.TryGetValue(headerName, out var headerValue)
                   ? headerValue
                   : null;

        public Task<string> ReadAsStringAsync() => Task.FromResult(_body);
    }
}
