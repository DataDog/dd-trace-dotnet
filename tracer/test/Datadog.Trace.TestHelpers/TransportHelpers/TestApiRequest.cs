// <copyright file="TestApiRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;

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

    public Uri Endpoint { get; }

    public string ContentType { get; private set; }

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
        ContentType = contentType;

        return Task.FromResult((IApiResponse)response);
    }

    public async Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
    {
        using (var ms = new MemoryStream())
        {
            await writeToRequestStream(ms);
            return await PostAsync(new ArraySegment<byte>(ms.ToArray()), ContentTypeHelper.GetContentType(contentType, multipartBoundary), contentEncoding);
        }
    }

    public async Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
    {
        var boundary = "----not implemented" + Guid.NewGuid().ToString("N");
        var contentType = ContentTypeHelper.GetContentType("multipart/form-data", boundary);

        return await PostAsync(
                   async stream =>
                   {
                       using var writer = new StreamWriter(stream, Encoding.UTF8);
                       await writer.WriteAsync("not implemented \r\n");
                   },
                   contentType,
                   "utf-8",
                   boundary);
    }
}
