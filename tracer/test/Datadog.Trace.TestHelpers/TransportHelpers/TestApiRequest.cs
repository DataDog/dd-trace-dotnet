// <copyright file="TestApiRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.TestHelpers.TransportHelpers;

internal class TestApiRequest : IApiRequest
{
    private readonly int _statusCode;
    private readonly string _responseContent;
    private readonly string _responseContentType;

    public TestApiRequest(
        Uri endpoint,
        int statusCode = 200,
        string responseContent = "{}",
        string responseContentType = "application/json")
    {
        _statusCode = statusCode;
        _responseContent = responseContent;
        _responseContentType = responseContentType;
        Endpoint = endpoint;
    }

    public bool UseGzip { get; set; }

    public Uri Endpoint { get; }

    public Dictionary<string, string> ExtraHeaders { get; } = new();

    public List<TestApiResponse> Responses { get; } = new();

    public void AddHeader(string name, string value)
    {
        ExtraHeaders.Add(name, value);
    }

    public virtual Task<IApiResponse> GetAsync()
    {
        var response = new TestApiResponse(_statusCode, _responseContent, _responseContentType);
        Responses.Add(response);

        return Task.FromResult((IApiResponse)response);
    }

    public Task<IApiResponse> PostAsync(ArraySegment<byte> traces, string contentType)
        => PostAsync(traces, contentType, contentEncoding: null);

    public virtual Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
    {
        var response = new TestApiResponse(_statusCode, _responseContent, _responseContentType);
        Responses.Add(response);

        return Task.FromResult((IApiResponse)response);
    }
}
