// <copyright file="TestRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using Datadog.Trace.Agent;
using Datadog.Trace.Util;

namespace Datadog.Trace.TestHelpers.TransportHelpers;

internal class TestRequestFactory : IApiRequestFactory
{
    private readonly Uri _baseEndpoint;
    private readonly Func<Uri, TestApiRequest>[] _requestsToSend;

    public TestRequestFactory(params Func<Uri, TestApiRequest>[] requestsToSend)
        : this(new Uri("http://localhost"), requestsToSend)
    {
    }

    public TestRequestFactory(Uri baseEndpoint, params Func<Uri, TestApiRequest>[] requestsToSend)
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
}
